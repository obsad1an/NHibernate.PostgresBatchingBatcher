using System;

namespace NHibernate.PostgresBatchingBatcher
{
    public class Helpers
    {
        /// <summary>
        /// Recognize if the input string is an insert or an update statement
        /// </summary>
        /// <param name="commandText">string to parse</param>
        /// <returns>true if the statement is to be batched, otherwise false</returns>
        internal static bool IsBatchable(string commandText)
        {
            //if the command is an insert
            if (commandText.StartsWith("INSERT INTO", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            //if the command is an select
            if (commandText.StartsWith("UPDATE"))
            {
                var idxWhere = commandText.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase);
                if (idxWhere == -1)
                {
                    return false;
                }
                var whereStatement = commandText.Substring(idxWhere);

                if (whereStatement.ToLower().Contains("and") || whereStatement.ToLower().Contains("or"))
                {
                    return false;
                }
                return true;
            }
            return false;
        }

        /// <summary> WHERE Statement process </summary>
        /// <remarks> This code handles only where the WHERE statement has one parameter without ANDs or ORs (What in most batch worthy cases is) </remarks>
        /// <param name="whereString">incoming where statement</param>
        /// <param name="whereParam">parameter to attach to the statement</param>
        /// <returns>where statement ready to be processed by Postgres</returns>
        internal static string ProcessWhere(string whereString, string whereParam)
        {
            var whereProperty = whereString.Trim().Split(' ')[1];

            return " WHERE " + whereProperty + " = " + whereParam + "; ";
        }
    }
}
