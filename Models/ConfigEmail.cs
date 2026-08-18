using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PIX.Models
{
    public class ConfigEmail
    {
        public string usuario { get; set; }
        public string senha { get; set; }
        public string servidorSMTP { get; set; }
        public int porta { get; set; }
        public string Email { get; set; }
        public string EmailCC { get; set; }

    }
}
