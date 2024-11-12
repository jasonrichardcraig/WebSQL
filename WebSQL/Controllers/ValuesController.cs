using System.Collections.Generic;
using System.Data.SqlClient;
using System.Runtime.InteropServices;
using System.Web.Http;

namespace WebSQL
{
    public class ValuesController : ApiController
    {
        // GET api/values 
        public IEnumerable<string> Get(int id)
        {
            using (var sqlConnection = new SqlConnection("Data Source=(local);Initial Catalog=WebSQL;Integrated Security=True;Encrypt=False"))
            {
                var sqlCommand = new SqlCommand($@"
                                            DECLARE @Start INT = 1;  -- Starting value
                                            DECLARE @End INT = {id};    -- Ending value

                                            WITH HelloWorld AS (
                                                SELECT @Start AS Number, CONCAT('Hello, World ', @Start) AS Message
                                                UNION ALL
                                                SELECT Number + 1, CONCAT('Hello, World ', Number + 1)
                                                FROM HelloWorld
                                                WHERE Number + 1 <= @End
                                            )
                                            SELECT Message
                                            FROM HelloWorld;", sqlConnection);

                sqlConnection.Open();

                using (var sqlDataReader = sqlCommand.ExecuteReader())
                {
                    while (sqlDataReader.Read())
                    {
                        yield return sqlDataReader.GetString(0);
                    }
                }
                sqlConnection.Close();
            }
        }



        // POST api/values 
        public void Post([FromBody] string value)
        {
        }

        // PUT api/values/5 
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/values/5 
        public void Delete(int id)
        {
        }
    }
}
