using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using PIX.Models;
using PIX.Repositories;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PIX.Services
{
    public class PIXBackgroundService : BackgroundService
    {
        private readonly PIXRepository _repository;
        private readonly string _strConnect;
        private bool tarefa_ativa = false;
        public PIXBackgroundService(PIXRepository repository, IConfiguration config)
        {
            _repository = repository;
            _strConnect = config.GetConnectionString("DeafultConnectionStrings") + "@DTILGCF06FW";
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {            
            Timer timer = new Timer(RegistraPIX, null, 1, Convert.ToInt16(TimeSpan.FromSeconds(5)));
        }

        private void RegistraPIX(object state)
        {
            if (!tarefa_ativa)
            {
                DataTable dtt_tran = new DataTable();
                string str_retorno = "";
                Pedido pedido = new Pedido();

                using (SqlConnection conn = new SqlConnection(_strConnect))
                {
                    conn.Open();
                    Console.WriteLine("Iniciou Tarefa!");

                    dtt_tran = _repository.ConsultaTransacaoPIX(0, "0", 0, 0, 0, Convert.ToDateTime("2001/01/01"), Convert.ToDateTime("2001/01/01"), -2, conn);
                    if (dtt_tran.Rows.Count > 0)
                    {
                        for (int i = 0; i < dtt_tran.Rows.Count; i++)
                        {
                            str_retorno = _repository.RegistraQrcodeErp(pedido);
                        }
                    }
                    Console.WriteLine("Finalizou Tarefa!");
                }
                tarefa_ativa = false;
            }            

        }

    }
}
