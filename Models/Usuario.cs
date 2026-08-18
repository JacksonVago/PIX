using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PIX.Models
{
    public class Usuario
    {
        public Int64 id { get; set; }

        public string username { get; set; }

        public string password { get; set; }

        public DateTime validade { get; set; }
    }
}
