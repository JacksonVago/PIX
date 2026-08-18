using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PIX.Models
{
    public class Paginacao
    {
        public int paginaAtual { get; set; }
        public int itensPorPagina { get; set; }
        public int quantidadeDePaginas { get; set; }
        public int quantidadeTotalDeItens { get; set; }
    }
}
