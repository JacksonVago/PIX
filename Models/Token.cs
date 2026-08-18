using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PIX.Models
{
    public class Token
    {
        public string access_token { get; set; }
        public string token_type { get; set; }
        public Int64 expires_in { get; set; }
    }
}
