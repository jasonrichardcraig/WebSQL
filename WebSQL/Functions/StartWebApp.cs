using System;
using System.Data.SqlTypes;
using WebSQL.Registries;

namespace WebSQL.Functions
{
    public partial class WebAppManager
    {
        [Microsoft.SqlServer.Server.SqlFunction]
        public static SqlBoolean StartWebApp(SqlString baseAddress)
        {

            if (baseAddress.IsNull)
            {
                throw new ArgumentNullException(nameof(baseAddress));
            }

            return ServiceRegistry.StartWebApp(baseAddress.Value);
        }
    }
}
