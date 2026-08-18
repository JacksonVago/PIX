using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PIX.Models
{
    public class ParametrosItau
    {
        public DateTime inicio { get; set; }
        public DateTime fim { get; set; }        
        public Paginacao paginacao { get; set; }
        public string status { get; set; }

        public ParametrosItau()
        {
            paginacao = new Paginacao();
        }
    }
}
