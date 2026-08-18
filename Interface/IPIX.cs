using PIX.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PIX.Interface
{
    public interface IPIX
    {
        string RegistraPedidoPIX(Pedido pedido);
        string RegistraTituloPIX(Int64 id_pix);
        string ConsultaPIX(Pedido pedido);
        string ConsultaListaPIX();
        string DevolucaoPIX(Pedido pedido);
        string ConsultaDevolucaoPIX(Pedido pedido);
        string RevisaPIX(Pedido pedido);
    }
}
