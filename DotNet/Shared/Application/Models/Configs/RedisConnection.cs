using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LantanaGroup.Link.Shared.Application.Models.Configs
{
    public class RedisConnection
    {
        public string Endpoint { get; set; }
        public string DatabaseName { get; set; }
        public int? DatabaseNumber { get; set; }
        public string Expiry { get; set; }
    }
}
