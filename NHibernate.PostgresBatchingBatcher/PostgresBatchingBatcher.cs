using System;
using System.Collections;
using System.Data;
using System.Diagnostics;
using System.Text;
using NHibernate.AdoNet;
using Npgsql;

namespace NHibernate.PostgresBatchingBatcher
{
    /// <summary> Custom Postgres batcher implementation </summary>
    public class PostgresBatchingBatcher : AbstractBatcher
    {
        #region private members
        private int _batchSize, _countOfCommands, _totalExpectedRowsAffected, _mParameterCounter;
        private StringBuilder _sbBatchCommand;
        private IDbCommand _currentBatch;
        #endregion private members

        public PostgresBatchingBatcher(ConnectionManager connectionManager, IInterceptor interceptor)
            : base(connectionManager, interceptor)
        {
            _batchSize = Factory.Settings.AdoBatchSize;
        }

        #region AbstractBatcher overridden methods and properties

        /// <summary> Adds the expected row count into the batch. </summary>
        /// <param name="expectation">The number of rows expected to be affected by the query.</param>
        /// <remarks>
        ///     If Batching is not supported, then this is when the Command should be executed.
        ///     If Batching is supported then it should hold of on executing the batch until
        ///     explicitly told to.
        /// </remarks>
        public override void AddToBatch(IExpectation expectation)
        {
            if (expectation.CanBeBatched && !Helpers.IsBatchable(CurrentCommand.CommandText))
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

            //Batch INSERT statements
            if (CurrentCommand.CommandText.StartsWith("INSERT INTO", StringComparison.OrdinalIgnoreCase))
            {
                BatchInsert();
            }
            //Batch UPDATE statements
            if (CurrentCommand.CommandText.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase))
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

                CheckReaders();

                //set prepared batchCommandText
                _currentBatch.CommandText = _sbBatchCommand.ToString();

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
                    {
                        Debugger.Break();
                    }
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

        #endregion AbstractBatcher overridden methods and properties



        #region private methods
        /// <summary>
        ///     generate the insert batch statement
        /// </summary>
        private void BatchInsert()
        {
            var len = CurrentCommand.CommandText.Length;
            var idx = CurrentCommand.CommandText.IndexOf("VALUES", StringComparison.OrdinalIgnoreCase);
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

                string param;
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

        /// <summary> generates the update batch statement </summary>
        private void BatchUpdate()
        {
            var len = CurrentCommand.CommandText.Length;
            var idx = CurrentCommand.CommandText.IndexOf("SET", StringComparison.OrdinalIgnoreCase);
            var endidx = idx + "SET".Length + 2;
            
            // WHERE is a reserved word. This is safe to make so far there are no set with special expressions
            var where = CurrentCommand.CommandText.IndexOf("WHERE ", StringComparison.OrdinalIgnoreCase);

            //check that a new batch has been started (DoExecuteBatch() reinitializes properties)
            if (_currentBatch == null)
            {
                _countOfCommands = 0;
                //new update in batch
                _currentBatch = new NpgsqlCommand();
                _sbBatchCommand = new StringBuilder();
                _mParameterCounter = 0;
            }

            var preCommand = CurrentCommand.CommandText.Substring(0, endidx - 1);
            _sbBatchCommand.Append(preCommand);//"UPDATE ..."

            //append values from CurrentCommand to _sbBatchCommand
            var values = CurrentCommand.CommandText.Substring(endidx - 1, where - endidx - 1);
            //get all values
            var split = values.Split(',');

            var columnParams = new string[split.Length];
            var columnNames = new string[split.Length];

            for (var i = 0; i < split.Length; i++)
            {
                columnParams[i] = split[i].Split('=')[1];
                columnNames[i] = split[i].Split('=')[0];
            }

            var paramName = new ArrayList(columnParams.Length);
            for (var i = 0; i < columnParams.Length; i++)
            {
                if (i != 0)
                    _sbBatchCommand.Append(", ");

                string param;
                if (columnParams[i].StartsWith(":"))   //first named parameter
                {
                    param = NextParam();
                    paramName.Add(param);
                }
                else if (columnParams[i].StartsWith(" :")) //other named parameter
                {
                    param = NextParam();
                    paramName.Add(param);
                }
                else if (columnParams[i].StartsWith(" "))  //other fix parameter
                {
                    param = columnParams[i].Substring(1, columnParams[i].Length - 1);
                }
                else
                {
                    param = columnParams[i];   //first fix parameter
                }

                _sbBatchCommand.Append(columnNames[i] + " = " + param);
            }
            var whereParam = NextParam();
            paramName.Add(whereParam);
            //append where
            var whereStatement = Helpers.ProcessWhere(CurrentCommand.CommandText.Substring(where - 1, len - where - 1), whereParam);
            _sbBatchCommand.Append(whereStatement);

            //rename & copy parameters from CurrentCommand to _currentBatch
            var iParam = 0;
            foreach (NpgsqlParameter param in CurrentCommand.Parameters)
            {
                param.ParameterName = (string)paramName[iParam++];

                var newParam = /*Clone()*/new NpgsqlParameter(param.ParameterName, param.NpgsqlDbType, param.Size, param.SourceColumn, param.Direction, param.IsNullable, param.Precision, param.Scale, param.SourceVersion, param.Value);
                _currentBatch.Parameters.Add(newParam);
            }

        }

        /// <summary> Next parameter factory </summary>
        /// <returns> Next parameter to be used </returns>
        private string NextParam()
        {
            return ":p" + _mParameterCounter++;
        }

        #endregion private methods
    }
}