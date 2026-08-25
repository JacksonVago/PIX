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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace PIX.Services
{
    public class PIXHS : IHostedService
    {
        private readonly PIXItau _repItau;
        private readonly PIXRepository _repBradesco;
        private readonly DataRepository _repData;
        private readonly string _strConnect;
        private Timer time;
        private bool tarefa_ativa = false;
        private bool _execConsultaLista = false;
        private readonly ILogger _logger;

        public PIXHS(DataRepository repository, PIXItau pixIt, PIXRepository pixBra, IConfiguration config, ILogger<PIXHostedService> log)
        {
            _repData = repository;
            _strConnect = config.GetConnectionString("DeafultConnectionStrings") + "@DTILGCF06FW";
            _logger = log;
            _repItau = pixIt;
            _repBradesco = pixBra;
            _execConsultaLista = Convert.ToBoolean(config.GetSection("ConfigPIX")["ConsultaListaAut"]);

        }
        public Task StartAsync(CancellationToken cancellationToken)
        {
            time = new Timer(ProcessaPIX, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
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
            catch (Exception ex)
            {
                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Erro StopAsync : " + ex.Message.ToString());
                throw new NotImplementedException();
            }

        }

        private void ProcessaPIX(object state)
        {
            //tarefa_ativa = true;
            if (!tarefa_ativa)
            {
                tarefa_ativa = true;
                DataTable dtt_tran = new DataTable();
                DataTable dtt_banco = new DataTable();
                string str_retorno = "";
                string str_classe = "PIX";
                Pedido pedido = new Pedido();
                Int64 int6_banco = 0;
                List<Params> filtros = new List<Params>();

                using (SqlConnection conn = new SqlConnection(_strConnect))
                {                                            
                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- Inicia registro de PIX´s");
                    //_logger.LogInformation(DateTime.Now.ToString("G") + "--Conexão " + _strConnect);
                    conn.Open();

                    try
                    {
                        //Processa PIX não registrados
                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- Identifica o banco que processará o PIX");
                        filtros.Add(new Params { nome = "id", valor = "724748", tipo = typeof(Int64).Name });
                        //filtros.Add(new Params { nome = "id", valor = "0", tipo = typeof(Int64).Name });
                        filtros.Add(new Params { nome = "id_empresa", valor = "0", tipo = typeof(Int64).Name });
                        filtros.Add(new Params { nome = "cnpj", valor = "0", tipo = typeof(Int64).Name });
                        filtros.Add(new Params { nome = "banco", valor = "0", tipo = typeof(Int32).Name });
                        filtros.Add(new Params { nome = "chave_pix", valor = "", tipo = typeof(string).Name });
                        filtros.Add(new Params { nome = "str_txid", valor = "", tipo = typeof(string).Name });
                        filtros.Add(new Params { nome = "int_codfil", valor = "0", tipo = typeof(Int32).Name });
                        filtros.Add(new Params { nome = "str_tipoped", valor = "", tipo = typeof(string).Name });
                        filtros.Add(new Params { nome = "int_pedido", valor = "0", tipo = typeof(Int64).Name });
                        //filtros.Add(new Params { nome = "int_operador", valor = "0", tipo = typeof(Int32).Name });
                        //filtros.Add(new Params { nome = "int_caixa", valor = "0", tipo = typeof(Int16).Name });
                        //filtros.Add(new Params { nome = "Itens", valor = "0", tipo = typeof(Int16).Name });
                        //filtros.Add(new Params { nome = "situacao", valor = "-1", tipo = typeof(Int16).Name });                        
                        filtros.Add(new Params { nome = "int_operador", valor = "-1", tipo = typeof(Int32).Name });
                        filtros.Add(new Params { nome = "int_caixa", valor = "-2", tipo = typeof(Int16).Name });
                        filtros.Add(new Params { nome = "Itens", valor = "0", tipo = typeof(Int16).Name });
                        filtros.Add(new Params { nome = "situacao", valor = "0", tipo = typeof(Int16).Name });
                        filtros.Add(new Params { nome = "DtIni", valor = "2001-01-01", tipo = typeof(DateTime).Name });
                        filtros.Add(new Params { nome = "DtFim", valor = "2001-01-01", tipo = typeof(DateTime).Name });
                        

                        dtt_tran = _repData.ConsultaGenericaDtt(filtros, "ntv_p_sel_tbl_transacao_pix", conn);
                        if (dtt_tran.Rows.Count > 0)
                        {
                            for (int i = 0; i < dtt_tran.Rows.Count; i++)
                            {
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Processa o PIX " + dtt_tran.Rows[i]["id"].ToString());


                                pedido.Filial = Convert.ToInt32(dtt_tran.Rows[i]["int_filial"]);
                                pedido.Tipo = Convert.ToInt16(dtt_tran.Rows[i]["int_tipoped"]);
                                pedido.numpedido = Convert.ToInt64(dtt_tran.Rows[i]["int_pedido"]);

                                str_classe = "PIX.Repositories.PIX";
                                switch (Convert.ToInt32(dtt_tran.Rows[i]["int_banco"]))
                                {
                                    case 237:
                                        str_classe += "Bradesco";
                                        if (pedido.Tipo == 9)
                                        {
                                            _repBradesco.RegistraQrcodeTitBradesco(Convert.ToInt64(dtt_tran.Rows[i]["id"]));
                                        }
                                        else
                                        {
                                            _repBradesco.RegistraQrcodeErpBradesco(pedido);
                                        }
                                        break;

                                    case 341:
                                        str_classe += "Itau";
                                        if (pedido.Tipo == 9)
                                        {
                                            //Registra PIX de títulos
                                            _repItau.RegistraTituloPIX(Convert.ToInt64(dtt_tran.Rows[i]["id"]));
                                            //_repItau.ConsultaPIX(pedido);
                                        }
                                        else
                                        {
                                            //Registra PIX de pedidos
                                            _repItau.RegistraPedidoPIX(pedido);
                                        }
                                        
                                        break;
                                }
                                /*
                                Assembly Natividade = Assembly.LoadFrom(@"C:\Jackson\API\PIX\PIX\bin\Debug\netcoreapp2.2\PIX.dll");
                                Assembly PIX = Assembly.LoadFrom(@".\PIX.dll");

                                Type ClasseImporta = Natividade.GetType(str_classe);
                                object obj = Activator.CreateInstance(ClasseImporta);

                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- ExecutaMetodo : carrega parametros ");

                                pedido.Filial = Convert.ToInt32(dtt_tran.Rows[i]["int_filial"]);
                                pedido.Tipo = Convert.ToInt16(dtt_tran.Rows[i]["int_tipoped"]);
                                pedido.numpedido = Convert.ToInt64(dtt_tran.Rows[i]["int_pedido"]);

                                object[] paramMetodo = new object[1];

                                paramMetodo[0] = pedido;

                                MethodInfo Metodo = ClasseImporta.GetMethod("RegistraPedidoPIX");
                                str_retorno = (String)Metodo.Invoke(obj, paramMetodo);



                                _logger.LogInformation(DateTime.Now.ToString("G") + "--  Econtrou PIX´s :" + dtt_tran.Rows.Count.ToString());
                                _logger.LogInformation(DateTime.Now.ToString("G") + " Pedido :" + dtt_tran.Rows[i]["int_pedido"].ToString());
                                _logger.LogInformation(DateTime.Now.ToString("G") + " Retorno do registro :" + str_retorno);
                                */
                            }
                        }

                        /*
                        //Consulta Lista de PIX para efetuar confirmação de pagamentos/ confirmação de devoluções
                        if (_execConsultaLista)
                        {
                            _repItau.ConsultaListaPIX();
                        }

                        //Revisar PIX (remover do banco)                        
                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- Identifica o banco que processará o PIX");
                        if (filtros.Count > 0){
                            filtros.Clear();
                        }
                        filtros.Add(new Params { nome = "id", valor = "0", tipo = typeof(Int64).Name });
                        filtros.Add(new Params { nome = "id_empresa", valor = "0", tipo = typeof(Int64).Name });
                        filtros.Add(new Params { nome = "cnpj", valor = "0", tipo = typeof(Int64).Name });
                        filtros.Add(new Params { nome = "banco", valor = "0", tipo = typeof(Int32).Name });
                        filtros.Add(new Params { nome = "chave_pix", valor = "", tipo = typeof(string).Name });
                        filtros.Add(new Params { nome = "str_txid", valor = "", tipo = typeof(string).Name });
                        filtros.Add(new Params { nome = "int_codfil", valor = "0", tipo = typeof(Int32).Name });
                        filtros.Add(new Params { nome = "str_tipoped", valor = "", tipo = typeof(string).Name });
                        filtros.Add(new Params { nome = "int_pedido", valor = "0", tipo = typeof(Int64).Name });
                        filtros.Add(new Params { nome = "int_operador", valor = "-1", tipo = typeof(Int32).Name });
                        filtros.Add(new Params { nome = "int_caixa", valor = "-2", tipo = typeof(Int16).Name });
                        filtros.Add(new Params { nome = "Itens", valor = "0", tipo = typeof(Int16).Name });
                        filtros.Add(new Params { nome = "situacao", valor = "-3", tipo = typeof(Int16).Name });
                        filtros.Add(new Params { nome = "DtIni", valor = "2001-01-01", tipo = typeof(DateTime).Name });
                        filtros.Add(new Params { nome = "DtFim", valor = "2001-01-01", tipo = typeof(DateTime).Name });


                        dtt_tran = _repData.ConsultaGenericaDtt(filtros, "ntv_p_sel_tbl_transacao_pix", conn);
                        if (dtt_tran.Rows.Count > 0)
                        {
                            for (int i = 0; i < dtt_tran.Rows.Count; i++)
                            {
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Identifica o banco que processará o PIX");


                                pedido.Filial = Convert.ToInt32(dtt_tran.Rows[i]["int_filial"]);
                                pedido.Tipo = Convert.ToInt16(dtt_tran.Rows[i]["int_tipoped"]);
                                pedido.numpedido = Convert.ToInt64(dtt_tran.Rows[i]["int_pedido"]);

                                str_classe = "PIX.Repositories.PIX";
                                switch (Convert.ToInt32(dtt_tran.Rows[i]["int_banco"]))
                                {
                                    case 237:
                                        str_classe += "Bradesco";
                                        _repBradesco.RevisaQrcodeErpBradesco(pedido);
                                        break;

                                    case 341:
                                        str_classe += "Itau";
                                        _repItau.RevisaPIX(pedido);
                                        break;
                                }
                            }
                        }*/
                        

                        //Devolução PIX (devolver Valor)                        
                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- Devolução nIdentifica o banco que processará o PIX");
                        if (filtros.Count > 0)
                        {
                            filtros.Clear();
                        }
                        filtros.Add(new Params { nome = "id", valor = "724748", tipo = typeof(Int64).Name });
                        filtros.Add(new Params { nome = "id_empresa", valor = "0", tipo = typeof(Int64).Name });
                        filtros.Add(new Params { nome = "cnpj", valor = "0", tipo = typeof(Int64).Name });
                        filtros.Add(new Params { nome = "banco", valor = "0", tipo = typeof(Int32).Name });
                        filtros.Add(new Params { nome = "chave_pix", valor = "", tipo = typeof(string).Name });
                        filtros.Add(new Params { nome = "str_txid", valor = "", tipo = typeof(string).Name });
                        filtros.Add(new Params { nome = "int_codfil", valor = "0", tipo = typeof(Int32).Name });
                        filtros.Add(new Params { nome = "str_tipoped", valor = "", tipo = typeof(string).Name });
                        filtros.Add(new Params { nome = "int_pedido", valor = "0", tipo = typeof(Int64).Name });
                        filtros.Add(new Params { nome = "int_operador", valor = "-1", tipo = typeof(Int32).Name });
                        filtros.Add(new Params { nome = "int_caixa", valor = "-2", tipo = typeof(Int16).Name });
                        filtros.Add(new Params { nome = "Itens", valor = "0", tipo = typeof(Int16).Name });
                        //filtros.Add(new Params { nome = "situacao", valor = "-9", tipo = typeof(Int16).Name });
                        filtros.Add(new Params { nome = "situacao", valor = "2", tipo = typeof(Int16).Name });
                        filtros.Add(new Params { nome = "DtIni", valor = "2001-01-01", tipo = typeof(DateTime).Name });
                        filtros.Add(new Params { nome = "DtFim", valor = "2001-01-01", tipo = typeof(DateTime).Name });


                        dtt_tran = _repData.ConsultaGenericaDtt(filtros, "ntv_p_sel_tbl_transacao_pix", conn);
                        if (dtt_tran.Rows.Count > 0)
                        {
                            for (int i = 0; i < dtt_tran.Rows.Count; i++)
                            {
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Devolução Identifica o banco que processará o PIX");


                                pedido.Filial = Convert.ToInt32(dtt_tran.Rows[i]["int_filial"]);
                                pedido.Tipo = Convert.ToInt16(dtt_tran.Rows[i]["int_tipoped"]);
                                pedido.numpedido = Convert.ToInt64(dtt_tran.Rows[i]["int_pedido"]);

                                str_classe = "PIX.Repositories.PIX";
                                switch (Convert.ToInt32(dtt_tran.Rows[i]["int_banco"]))
                                {
                                    case 237:
                                        str_classe += "Bradesco";
                                        _repBradesco.DevolucaoQRCODE(pedido);
                                        break;

                                    case 341:
                                        str_classe += "Itau";
                                        //_repItau.DevolucaoPIX(pedido);
                                        _repItau.Async_DevolucaoPIX(pedido);
                                        break;
                                }
                            }
                        }

                        //Confirmas as Devoluções solicitadas ao banco
                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- Identifica o banco que processará o PIX");
                        if (filtros.Count > 0)
                        {
                            filtros.Clear();
                        }
                        filtros.Add(new Params { nome = "id", valor = "724748", tipo = typeof(Int64).Name });
                        filtros.Add(new Params { nome = "id_empresa", valor = "0", tipo = typeof(Int64).Name });
                        filtros.Add(new Params { nome = "cnpj", valor = "0", tipo = typeof(Int64).Name });
                        filtros.Add(new Params { nome = "banco", valor = "0", tipo = typeof(Int32).Name });
                        filtros.Add(new Params { nome = "chave_pix", valor = "", tipo = typeof(string).Name });
                        filtros.Add(new Params { nome = "str_txid", valor = "", tipo = typeof(string).Name });
                        filtros.Add(new Params { nome = "int_codfil", valor = "0", tipo = typeof(Int32).Name });
                        filtros.Add(new Params { nome = "str_tipoped", valor = "", tipo = typeof(string).Name });
                        filtros.Add(new Params { nome = "int_pedido", valor = "0", tipo = typeof(Int64).Name });
                        filtros.Add(new Params { nome = "int_operador", valor = "-1", tipo = typeof(Int32).Name });
                        filtros.Add(new Params { nome = "int_caixa", valor = "-2", tipo = typeof(Int16).Name });
                        filtros.Add(new Params { nome = "Itens", valor = "0", tipo = typeof(Int16).Name });
                        filtros.Add(new Params { nome = "situacao", valor = "5", tipo = typeof(Int16).Name });
                        filtros.Add(new Params { nome = "DtIni", valor = "2001-01-01", tipo = typeof(DateTime).Name });
                        filtros.Add(new Params { nome = "DtFim", valor = "2001-01-01", tipo = typeof(DateTime).Name });


                        dtt_tran = _repData.ConsultaGenericaDtt(filtros, "ntv_p_sel_tbl_transacao_pix", conn);
                        if (dtt_tran.Rows.Count > 0)
                        {
                            for (int i = 0; i < dtt_tran.Rows.Count; i++)
                            {
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Identifica o banco que processará o PIX");


                                pedido.Filial = Convert.ToInt32(dtt_tran.Rows[i]["int_filial"]);
                                pedido.Tipo = Convert.ToInt16(dtt_tran.Rows[i]["int_tipoped"]);
                                pedido.numpedido = Convert.ToInt64(dtt_tran.Rows[i]["int_pedido"]);

                                str_classe = "PIX.Repositories.PIX";
                                switch (Convert.ToInt32(dtt_tran.Rows[i]["int_banco"]))
                                {
                                    case 237:
                                        str_classe += "Bradesco";
                                        _repBradesco.DevolucaoQRCODE(pedido);
                                        break;

                                    case 341:
                                        str_classe += "Itau";
                                        _repItau.ConfirmaDevolucaoPIX(pedido);
                                        break;
                                }
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
