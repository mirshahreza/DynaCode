
using System.Text.Encodings;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using AppEndDbIO;

namespace AppEndDbIO
{
    public static class ClientQueryHandler
    {
        public static object? Exec(JsonElement ClientQueryJson)
        {

            ClientQuery? clientQuery = JsonSerializer.Deserialize<ClientQuery>(ClientQueryJson);
            if (clientQuery == null) return false;
            clientQuery = ClientQuery.Instance("DynaCodeRoot", clientQuery);
            return clientQuery.Exec();
        }
    }
}
