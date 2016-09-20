using System;
using System.Collections;
using System.Data;
using System.Diagnostics;
using System.Text;
using NHibernate.AdoNet;
using Npgsql;

namespace NHibernate.PostgresBatcher
{
    /// <summary> Custom Postgres batcher implementation </summary>
    public class PostgresBatcher : AbstractBatcher
    {
        #region private members
        private int _batchSize, _countOfCommands, _totalExpectedRowsAffected, _mParameterCounter;
        private StringBuilder _sbBatchCommand;
        private IDbCommand _currentBatch;
        #endregion private members

        public PostgresBatcher(ConnectionManager connectionManager, IInterceptor interceptor)
            : base(connectionManager, interceptor)
        {
            _batchSize = Factory.Settings.AdoBatchSize;
        }

        private string NextParam()
        {
            return ":p" + _mParameterCounter++;
        }

        /// <summary> Adds the expected row count into the batch. </summary>
        /// <param name="expectation">The number of rows expected to be affected by the query.</param>
        /// <remarks>
        ///     If Batching is not supported, then this is when the Command should be executed.
        ///     If Batching is supported then it should hold of on executing the batch until
        ///     explicitly told to.
        /// </remarks>
        public override void AddToBatch(IExpectation expectation)
        {
            if (expectation.CanBeBatched && !((CurrentCommand.CommandText.StartsWith("INSERT INTO") && CurrentCommand.CommandText.Contains("VALUES")) || (CurrentCommand.CommandText.StartsWith("UPDATE") && CurrentCommand.CommandText.Contains("SET"))))
            {
                //NonBatching behavior
                var cmd = CurrentCommand;
                LogCommand(CurrentCommand);
                var rowCount = ExecuteNonQuery(cmd);
                expectation.VerifyOutcomeNonBatched(rowCount, cmd);
                _currentBatch = null;
                return;
            }

            _totalExpectedRowsAffected += expectation.ExpectedRowCount;
            //this.Info("Adding to batch");

            //Batch INSERT statements
            if (CurrentCommand.CommandText.StartsWith("INSERT INTO") && CurrentCommand.CommandText.Contains("VALUES"))
            {
                BatchInsert();
            }
            //Batch UPDATE statements
            if (CurrentCommand.CommandText.StartsWith("UPDATE") && CurrentCommand.CommandText.Contains("SET"))
            {
                BatchUpdate();
            }
            _countOfCommands++;
            //check for flush
            if (_countOfCommands >= _batchSize)
            {
                DoExecuteBatch(_currentBatch);
            }
        }

        protected override void DoExecuteBatch(IDbCommand ps)
        {
            if (_currentBatch != null)
            {
                //Batch command now needs its terminator
                _sbBatchCommand.Append(";");

                _countOfCommands = 0;

                //this.Info("Executing batch");
                CheckReaders();

                //set prepared batchCommandText
                var commandText = _sbBatchCommand.ToString();
                _currentBatch.CommandText = commandText;

                LogCommand(_currentBatch);

                Prepare(_currentBatch);

                int rowsAffected;
                try
                {
                    rowsAffected = _currentBatch.ExecuteNonQuery();
                }
                catch (Exception)
                {
                    if (Debugger.IsAttached)
                        Debugger.Break();
                    throw;
                }

                Expectations.VerifyOutcomeBatched(_totalExpectedRowsAffected, rowsAffected);

                _totalExpectedRowsAffected = 0;
                _currentBatch = null;
                _sbBatchCommand = null;
                _mParameterCounter = 0;
            }
        }

        protected override int CountOfStatementsInCurrentBatch
        {
            get { return _countOfCommands; }
        }

        public override int BatchSize
        {
            get { return _batchSize; }
            set { _batchSize = value; }
        }

        /// <summary>
        ///     generate the insert batch statement
        /// </summary>
        private void BatchInsert()
        {
            var len = CurrentCommand.CommandText.Length;
            var idx = CurrentCommand.CommandText.IndexOf("VALUES", StringComparison.Ordinal);
            var endidx = idx + "VALUES".Length + 2;

            if (_currentBatch == null)
            {
                // begin new batch. 
                _currentBatch = new NpgsqlCommand();
                _sbBatchCommand = new StringBuilder();
                _mParameterCounter = 0;

                var preCommand = CurrentCommand.CommandText.Substring(0, endidx);
                _sbBatchCommand.Append(preCommand);
            }
            else
            {
                //only append Values
                _sbBatchCommand.Append(", (");
            }

            //append values from CurrentCommand to _sbBatchCommand
            var values = CurrentCommand.CommandText.Substring(endidx, len - endidx - 1);
            //get all values
            var split = values.Split(',');

            var paramName = new ArrayList(split.Length);
            for (var i = 0; i < split.Length; i++)
            {
                if (i != 0)
                    _sbBatchCommand.Append(", ");

                string param = null;
                if (split[i].StartsWith(":"))   //first named parameter
                {
                    param = NextParam();
                    paramName.Add(param);
                }
                else if (split[i].StartsWith(" :")) //other named parameter
                {
                    param = NextParam();
                    paramName.Add(param);
                }
                else if (split[i].StartsWith(" "))  //other fix parameter
                {
                    param = split[i].Substring(1, split[i].Length - 1);
                }
                else
                {
                    param = split[i];   //first fix parameter
                }

                _sbBatchCommand.Append(param);
            }
            _sbBatchCommand.Append(")");

            //rename & copy parameters from CurrentCommand to _currentBatch
            var iParam = 0;
            foreach (NpgsqlParameter param in CurrentCommand.Parameters)
            {
                param.ParameterName = (string)paramName[iParam++];

                var newParam = /*Clone()*/new NpgsqlParameter(param.ParameterName, param.NpgsqlDbType, param.Size, param.SourceColumn, param.Direction, param.IsNullable, param.Precision, param.Scale, param.SourceVersion, param.Value);
                _currentBatch.Parameters.Add(newParam);
            }
        }

        /// <summary>
        /// generates the update batch statement
        /// </summary>
        private void BatchUpdate()
        {
            //TODO: IMPLEMENT
        }
    }
}