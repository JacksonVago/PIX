using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    public class PIXHostedService : IHostedService
    {
        private readonly PIXRepository _repository;
        private readonly string _strConnect;
        private Timer time;
        private bool tarefa_ativa = false;
        private readonly ILogger _logger;

        public PIXHostedService(PIXRepository repository, IConfiguration config, ILogger<PIXHostedService> log)
        {
            _repository = repository;
            _strConnect = config.GetConnectionString("DeafultConnectionStrings") + "@DTILGCF06FW";
            _logger = log;

        }
        public Task StartAsync(CancellationToken cancellationToken)
        {
            time = new Timer(RegistraPIX,null,TimeSpan.Zero,TimeSpan.FromSeconds(5));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation(DateTime.Now.ToString("G") + "-- Inicio StopAsync : " + cancellationToken.ToString());
            try
            {
                //time = new Timer(RegistraPIX, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));                
                //return StartAsync(cancellationToken);
                return Task.CompletedTask;
                //throw new NotImplementedException();
            }
            catch(Exception ex)
            {
                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Erro StopAsync : " + ex.Message.ToString());
                throw new NotImplementedException();
            }
            
        }

        private void RegistraPIX(object state)
        {
            //tarefa_ativa = true;
            if (!tarefa_ativa)
            {
               tarefa_ativa = true;
                DataTable dtt_tran = new DataTable();
                string str_retorno = "";
                Pedido pedido = new Pedido();

                using (SqlConnection conn = new SqlConnection(_strConnect))
                {
                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- Inicia registro de PIX´s");
                    conn.Open();

                    try
                    {
                        //dtt_tran = _repository.ConsultaUsuarios("jackson", conn);

                        //Processa PIX não registrados
                        dtt_tran = _repository.ConsultaTransacaoPIX(0, "", 0, StatusPIX.Aguardando_registro, 0, Convert.ToDateTime("2001-01-01"), Convert.ToDateTime("2001-01-01"), -2, conn);
                        if (dtt_tran.Rows.Count > 0)
                        {
                            _logger.LogInformation(DateTime.Now.ToString("G") + "--  Econtrou PIX´s :" + dtt_tran.Rows.Count.ToString());
                            for (int i = 0; i < dtt_tran.Rows.Count; i++)
                            {
                                _logger.LogInformation(DateTime.Now.ToString("G") + " Pedido :" + dtt_tran.Rows[i]["int_pedido"].ToString());
                                pedido.Filial = Convert.ToInt32(dtt_tran.Rows[i]["int_filial"]);
                                pedido.Tipo = Convert.ToInt16(dtt_tran.Rows[i]["int_tipoped"]);
                                pedido.numpedido = Convert.ToInt64(dtt_tran.Rows[i]["int_pedido"]);
                                //Banco Bradesco
                                str_retorno = _repository.RegistraQrcodeErpBradesco(pedido);
                                _logger.LogInformation(DateTime.Now.ToString("G") + " Retorno do registro :" + str_retorno);
                                //Banco do Brasil
                                //str_retorno = _repository.RegistraQrcodeErp(pedido);
                                //str_retorno = _repository.ConsultaQRCODE(pedido);
                                //str_retorno = _repository.GeraQrcodePedido(pedido);
                                //str_retorno = _repository.PutWebHook();
                                //str_retorno = _repository.GetWebHook();
                            }
                        }

                        //Processa QR codes aguardando pagamento                    
                        dtt_tran = _repository.ConsultaTransacaoPIX(0, "", 0, StatusPIX.Ativa, 0, Convert.ToDateTime("2001-01-01"), Convert.ToDateTime("2001-01-01"), -2, conn);
                        if (dtt_tran.Rows.Count > 0)
                        {
                            _logger.LogInformation(DateTime.Now.ToString("G") + " PIX´s gerados aguardando QRCODE :" + dtt_tran.Rows.Count.ToString());
                            for (int i = 0; i < dtt_tran.Rows.Count; i++)
                            {
                                _logger.LogInformation(DateTime.Now.ToString("G") + " Pedido :" + dtt_tran.Rows[i]["int_pedido"].ToString());
                                pedido.Filial = Convert.ToInt32(dtt_tran.Rows[i]["int_filial"]);
                                pedido.Tipo = Convert.ToInt16(dtt_tran.Rows[i]["int_tipoped"]);
                                pedido.numpedido = Convert.ToInt64(dtt_tran.Rows[i]["int_pedido"]);
                                //Banco Bradesco
                                str_retorno = _repository.ConsultaQRCODEBradesco(pedido);
                                _logger.LogInformation(DateTime.Now.ToString("G") + " PIX´s QRCODE gerado (:" + dtt_tran.Rows.Count.ToString() + ") " + str_retorno);
                                //str_retorno = _repository.DevolucaoQRCODE(pedido);                            
                                //str_retorno = _repository.ConsDevolucaoQRCODE(pedido);
                                //str_retorno = _repository.ConsultaListaQRCODE();
                            }
                        }

                        //Processa devolução de QR codes pagos
                        dtt_tran = _repository.ConsultaTransacaoPIX(0, "", 0, StatusPIX.Devolver, 0, Convert.ToDateTime("2001-01-01"), Convert.ToDateTime("2001-01-01"), -2, conn);
                        if (dtt_tran.Rows.Count > 0)
                        {
                            _logger.LogInformation(DateTime.Now.ToString("G") + " Devoluções de QRCODE :" + dtt_tran.Rows.Count.ToString());
                            for (int i = 0; i < dtt_tran.Rows.Count; i++)
                            {
                                _logger.LogInformation(DateTime.Now.ToString("G") + " Pedido :" + dtt_tran.Rows[i]["int_pedido"].ToString());
                                pedido.Filial = Convert.ToInt32(dtt_tran.Rows[i]["int_filial"]);
                                pedido.Tipo = Convert.ToInt16(dtt_tran.Rows[i]["int_tipoped"]);
                                pedido.numpedido = Convert.ToInt64(dtt_tran.Rows[i]["int_pedido"]);
                                //Banco Bradesco
                                str_retorno = _repository.DevolucaoQRCODE(pedido);
                                _logger.LogInformation(DateTime.Now.ToString("G") + " PIX´s QRCODE devolvidos (:" + dtt_tran.Rows.Count.ToString() + ") " + str_retorno);
                                //str_retorno = _repository.DevolucaoQRCODE(pedido);                            
                                //str_retorno = _repository.ConsDevolucaoQRCODE(pedido);
                                //str_retorno = _repository.ConsultaListaQRCODE();
                            }
                        }

                        //Remove QR codes pagos do banco
                        dtt_tran = _repository.ConsultaTransacaoPIX(0, "", 0, StatusPIX.Remover, 0, Convert.ToDateTime("2001-01-01"), Convert.ToDateTime("2001-01-01"), -2, conn);
                        if (dtt_tran.Rows.Count > 0)
                        {
                            _logger.LogInformation(DateTime.Now.ToString("G") + " Remoções de QRCODE :" + dtt_tran.Rows.Count.ToString());
                            for (int i = 0; i < dtt_tran.Rows.Count; i++)
                            {
                                _logger.LogInformation(DateTime.Now.ToString("G") + " Pedido :" + dtt_tran.Rows[i]["int_pedido"].ToString());
                                pedido.Filial = Convert.ToInt32(dtt_tran.Rows[i]["int_filial"]);
                                pedido.Tipo = Convert.ToInt16(dtt_tran.Rows[i]["int_tipoped"]);
                                pedido.numpedido = Convert.ToInt64(dtt_tran.Rows[i]["int_pedido"]);
                                //Banco Bradesco
                                str_retorno = _repository.RevisaQrcodeErpBradesco(pedido);
                                _logger.LogInformation(DateTime.Now.ToString("G") + " PIX´s QRCODE removidos (:" + dtt_tran.Rows.Count.ToString() + ") " + str_retorno);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInformation(DateTime.Now.ToString("G") + " Hostedservice erro :" + ex.Message.ToString());
                    }

                    conn.Close();
                    tarefa_ativa = false;
                }
            }

        }
    }
}
