using System;
using System.Data.SqlTypes;
using WebSQL.Registries;

namespace WebSQL.Functions
{
    public partial class WebAppManager
    {
        [Microsoft.SqlServer.Server.SqlFunction]
        public static SqlBoolean StopWebApp(SqlString baseAddress)
        {
            if (baseAddress.IsNull)
            {
                throw new ArgumentNullException(nameof(baseAddress));
            }

            return ServiceRegistry.StopWebApp(baseAddress.Value);
        }
    }
}
