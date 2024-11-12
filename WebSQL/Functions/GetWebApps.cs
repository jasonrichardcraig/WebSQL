using Microsoft.SqlServer.Server;
using System.Collections;
using System.Data.SqlTypes;
using System.Linq;
using WebSQL.Registries;

namespace WebSQL.Functions
{
    public partial class WebAppManager
    {
        [SqlFunction(FillRowMethodName = "GetWebApps_FillRow", TableDefinition = "BaseAddress nvarchar(255)")]
        public static IEnumerable GetWebApps()
        {
            ArrayList resultCollection = new ArrayList();
            ServiceRegistry.GetWebApps().ToList().ForEach(webApp => resultCollection.Add(new GetWebApps_Result(webApp)));
            return resultCollection;
        }

        internal class GetWebApps_Result
        {
            public SqlString BaseAddress;

            public GetWebApps_Result(SqlString baseAddress)
            {
                BaseAddress = baseAddress;
            }
        }

        public static void GetWebApps_FillRow(object getWebApps_ResultObj, out SqlString baseAddress)
        {
            var getWebApps_Result = (getWebApps_ResultObj as GetWebApps_Result);
            baseAddress = getWebApps_Result.BaseAddress;
        }

    }
}
