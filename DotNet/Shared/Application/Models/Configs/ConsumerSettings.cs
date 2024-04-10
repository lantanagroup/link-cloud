using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LantanaGroup.Link.Shared.Application.Models.Configs
{
    public class ConsumerSettings
    {
        public List<string> ConsumerRetryDuration { get; set; }
        public bool DisableRetryConsumer { get; set; }
        public bool DisableConsumer { get; set; }
    }
}
