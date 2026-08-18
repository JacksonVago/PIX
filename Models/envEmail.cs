using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PIX.Models
{
    public class envEmail
    {
        public Int64 id { get; set; }
        public Int64 id_assunto { get; set; }
        public string str_remet { get; set; }
        public string str_dest { get; set; }
        public string str_copias { get; set; }
        public string str_copias_oc { get; set; }
        public string str_corpo { get; set; }
        public string str_html { get; set; }
        public string str_anexo { get; set; }
        public DateTime dtm_inclusao { get; set; }
        public Int64 int_usuario { get; set; }
        public string str_erro { get; set; }
        public Int16 int_situacao { get; set; }
        public string str_assunto { get; set; }

    }
}
