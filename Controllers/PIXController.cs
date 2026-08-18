using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PIX.Models;
using PIX.Repositories;

namespace PIX.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PIXController : Controller
    {
        private readonly PIXRepository _repository;
        private readonly PIXItau _repItau;
        private readonly UsuarioRepository _usuarioRepository;
        private readonly IHttpContextAccessor _httpContext;
        private readonly ILogger _logger;

        public PIXController(PIXRepository repository, UsuarioRepository usuario, PIXItau repItau, IHttpContextAccessor contextAccessor, ILogger<PIXController> log)
        {
            _repository = repository;
            _usuarioRepository = usuario;
            _httpContext = contextAccessor;
            _logger = log;
            _repItau = repItau;
        }

        [HttpPost("v1/RegQRCODE")]
        public async Task<ActionResult<Mensagem>> RegistraQRCODE([FromBody] Pedido pedido)
        {
            string str_retorno = "";
            try
            {
                var ip = _httpContext.HttpContext.Connection.LocalIpAddress.ToString();
                var metodo = _httpContext.HttpContext.Request.Path.ToString();

                var stream = _httpContext.HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(stream);
                var tokenS = handler.ReadToken(stream) as JwtSecurityToken;
                //tokenS.Claims["id"]
                bool bolret = await _usuarioRepository.GravarAcesso(new Usuario { id = Convert.ToInt64(tokenS.Claims.ToArray()[1].Value), password = "", username = "", validade = DateTime.Now }, stream, ip, metodo, JsonConvert.SerializeObject(pedido));
                str_retorno = _repository.RegistraQrcodeErp(pedido);
                return (new Mensagem { mensagem = str_retorno.ToString() });
            }
            catch (Exception ex)
            {
                return NotFound(new { mensagem = ex.Message.ToString() });
            }

        }

        [HttpPost("v1/RevQRCODE")]
        public async Task<ActionResult<Mensagem>> RevisaQRCODE([FromBody] Pedido pedido)
        {
            string str_retorno = "";
            try
            {
                var ip = _httpContext.HttpContext.Connection.LocalIpAddress.ToString();
                var metodo = _httpContext.HttpContext.Request.Path.ToString();

                var stream = _httpContext.HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(stream);
                var tokenS = handler.ReadToken(stream) as JwtSecurityToken;
                //tokenS.Claims["id"]
                bool bolret = await _usuarioRepository.GravarAcesso(new Usuario { id = Convert.ToInt64(tokenS.Claims.ToArray()[1].Value), password = "", username = "", validade = DateTime.Now }, stream, ip, metodo, JsonConvert.SerializeObject(pedido));
                str_retorno = _repository.RevisaQrcodeErpBradesco(pedido);
                return (new Mensagem { mensagem = str_retorno.ToString() });
            }
            catch (Exception ex)
            {
                return NotFound(new { mensagem = ex.Message.ToString() });
            }
        }

        [HttpPost("v1/ConQRCODE")]
        public async Task<ActionResult<Mensagem>> ConsultaQRCODE([FromBody] Pedido pedido)
        {
            string str_retorno = "";
            try
            {
                var ip = _httpContext.HttpContext.Connection.LocalIpAddress.ToString();
                var metodo = _httpContext.HttpContext.Request.Path.ToString();

                var stream = _httpContext.HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(stream);
                var tokenS = handler.ReadToken(stream) as JwtSecurityToken;
                //tokenS.Claims["id"]
                bool bolret = await _usuarioRepository.GravarAcesso(new Usuario { id = Convert.ToInt64(tokenS.Claims.ToArray()[1].Value), password = "", username = "", validade = DateTime.Now }, stream, ip, metodo, JsonConvert.SerializeObject(pedido));
                str_retorno = _repository.ConsultaQRCODE(pedido);
                return (new Mensagem { mensagem = str_retorno.ToString() });
            }
            catch (Exception ex)
            {
                return NotFound(new { mensagem = ex.Message.ToString() });
            }
        }

        [HttpPost("v1/itau/ConQRCODE")]
        public async Task<ActionResult<Mensagem>> ConsultaQRCODEItau([FromBody] Pedido pedido)
        {
            string str_retorno = "";
            try
            {
                var ip = _httpContext.HttpContext.Connection.LocalIpAddress.ToString();
                var metodo = _httpContext.HttpContext.Request.Path.ToString();

                var stream = _httpContext.HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(stream);
                var tokenS = handler.ReadToken(stream) as JwtSecurityToken;
                //tokenS.Claims["id"]
                bool bolret = await _usuarioRepository.GravarAcesso(new Usuario { id = Convert.ToInt64(tokenS.Claims.ToArray()[1].Value), password = "", username = "", validade = DateTime.Now }, stream, ip, metodo, JsonConvert.SerializeObject(pedido));
                str_retorno = _repItau.ConsultaPIX(pedido);
                return (new Mensagem { mensagem = str_retorno.ToString() });
            }
            catch (Exception ex)
            {
                return NotFound(new { mensagem = ex.Message.ToString() });
            }
        }

        [HttpGet("v1/ConLQRCODE")]
        public async Task<ActionResult<Mensagem>> ConsultaListaQRCODE()
        {
            string str_retorno = "";
            try
            {
                var ip = _httpContext.HttpContext.Connection.LocalIpAddress.ToString();
                var metodo = _httpContext.HttpContext.Request.Path.ToString();

                var stream = _httpContext.HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(stream);
                var tokenS = handler.ReadToken(stream) as JwtSecurityToken;
                //tokenS.Claims["id"]
                bool bolret = await _usuarioRepository.GravarAcesso(new Usuario { id = Convert.ToInt64(tokenS.Claims.ToArray()[1].Value), password = "", username = "", validade = DateTime.Now }, stream, ip, metodo, "");
                str_retorno = _repository.ConsultaListaQRCODE();
                return (new Mensagem { mensagem = str_retorno.ToString() });
            }
            catch (Exception ex)
            {
                return NotFound(new { mensagem = ex.Message.ToString() });
            }
        }

        [HttpPost("v1/DevQRCODE")]
        public async Task<ActionResult<Mensagem>> DevolucaoQRCODE([FromBody] Pedido pedido)
        {
            string str_retorno = "";
            try
            {
                var ip = _httpContext.HttpContext.Connection.LocalIpAddress.ToString();
                var metodo = _httpContext.HttpContext.Request.Path.ToString();

                var stream = _httpContext.HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(stream);
                var tokenS = handler.ReadToken(stream) as JwtSecurityToken;
                //tokenS.Claims["id"]
                bool bolret = await _usuarioRepository.GravarAcesso(new Usuario { id = Convert.ToInt64(tokenS.Claims.ToArray()[1].Value), password = "", username = "", validade = DateTime.Now }, stream, ip, metodo, JsonConvert.SerializeObject(pedido));
                str_retorno = _repository.DevolucaoQRCODE(pedido);
                return (new Mensagem { mensagem = str_retorno.ToString() });
            }
            catch (Exception ex)
            {
                return NotFound(new { mensagem = ex.Message.ToString() });
            }
        }

        [HttpPost("v1/ConDevQRCODE")]
        public async Task<ActionResult<Mensagem>> ConsDevolucaoQRCODE([FromBody] Pedido pedido)
        {
            string str_retorno = "";
            try
            {
                var ip = _httpContext.HttpContext.Connection.LocalIpAddress.ToString();
                var metodo = _httpContext.HttpContext.Request.Path.ToString();

                var stream = _httpContext.HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(stream);
                var tokenS = handler.ReadToken(stream) as JwtSecurityToken;
                //tokenS.Claims["id"]
                bool bolret = await _usuarioRepository.GravarAcesso(new Usuario { id = Convert.ToInt64(tokenS.Claims.ToArray()[1].Value), password = "", username = "", validade = DateTime.Now }, stream, ip, metodo, JsonConvert.SerializeObject(pedido));
                str_retorno = _repository.ConsDevolucaoQRCODE(pedido);
                return (new Mensagem { mensagem = str_retorno.ToString() });
            }
            catch (Exception ex)
            {
                return NotFound(new { mensagem = ex.Message.ToString() });
            }
        }

        [HttpPost("v1/GerQRCODEPed")]
        public async Task<ActionResult<Mensagem>> GeraQRCODEPedido([FromBody] Pedido pedido)
        {
            string str_retorno = "";
            try
            {
                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- Gerar QR CODE pedido (" + pedido.numpedido.ToString() + ")");
                var ip = _httpContext.HttpContext.Connection.LocalIpAddress.ToString();
                var metodo = _httpContext.HttpContext.Request.Path.ToString();

                var stream = _httpContext.HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(stream);
                var tokenS = handler.ReadToken(stream) as JwtSecurityToken;
                //tokenS.Claims["id"]
                bool bolret = await _usuarioRepository.GravarAcesso(new Usuario { id = Convert.ToInt64(tokenS.Claims.ToArray()[1].Value), password = "", username = "", validade = DateTime.Now }, stream, ip, metodo, JsonConvert.SerializeObject(pedido));
                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Vai Gerar QR CODE pedido (" + pedido.numpedido.ToString() + ")");
                str_retorno = _repository.GeraQrcodePedido(pedido);
                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Gerou QR CODE pedido (" + pedido.numpedido.ToString() + ") " + str_retorno);
                return (new Mensagem { mensagem = str_retorno.ToString() });
            }
            catch (Exception ex)
            {
                return NotFound(new { mensagem = ex.Message.ToString() });
            }

        }

        [HttpPost("v1/GerQRCODETit")]
        public async Task<ActionResult<Mensagem>> GeraQRCODETitulo([FromBody] Pedido pedido)
        {
            string str_retorno = "";
            try
            {
                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Gerar QR CODE pedido (" + pedido.numpedido.ToString() + ")");
                var ip = _httpContext.HttpContext.Connection.LocalIpAddress.ToString();
                var metodo = _httpContext.HttpContext.Request.Path.ToString();

                var stream = _httpContext.HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(stream);
                var tokenS = handler.ReadToken(stream) as JwtSecurityToken;
                //tokenS.Claims["id"]
                bool bolret = await _usuarioRepository.GravarAcesso(new Usuario { id = Convert.ToInt64(tokenS.Claims.ToArray()[1].Value), password = "", username = "", validade = DateTime.Now }, stream, ip, metodo, JsonConvert.SerializeObject(pedido));
                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Vai Gerar QR CODE pedido (" + pedido.numpedido.ToString() + ")");
                str_retorno = _repository.GeraQrcodePedido(pedido);
                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Gerou QR CODE pedido (" + pedido.numpedido.ToString() + ") " + str_retorno);
                return (new Mensagem { mensagem = str_retorno.ToString() });
            }
            catch (Exception ex)
            {
                return NotFound(new { mensagem = ex.Message.ToString() });
            }

        }

        [HttpPost("v1/RegPIXTitulos")]
        public async Task<ActionResult<Mensagem>> RegTitQRCODE([FromQuery] Int64 id)
        {
            string str_retorno = "";
            try
            {
                var ip = _httpContext.HttpContext.Connection.LocalIpAddress.ToString();
                var metodo = _httpContext.HttpContext.Request.Path.ToString();

                var stream = _httpContext.HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(stream);
                var tokenS = handler.ReadToken(stream) as JwtSecurityToken;
                //tokenS.Claims["id"]
                bool bolret = await _usuarioRepository.GravarAcesso(new Usuario { id = Convert.ToInt64(tokenS.Claims.ToArray()[1].Value), password = "", username = "", validade = DateTime.Now }, stream, ip, metodo, id.ToString());
                str_retorno = _repository.RegistraQrcodeTitBradesco(id);
                if (str_retorno.Contains("sucesso"))
                {
                    //Gerar QR CODE
                    str_retorno = _repository.GeraQrcodePIX(id);
                    return (new Mensagem { mensagem = str_retorno.ToString() });
                }

                return (new Mensagem { mensagem = str_retorno.ToString() });
            }
            catch (Exception ex)
            {
                return NotFound(new { mensagem = ex.Message.ToString() });
            }

        }

        [HttpGet("v1/ConfirmaPagPIXItau")]
        public async Task<bool> ConfirmaPagPIXItau()
        {
            _repItau.ConsultaListaPIX();
            return true;
        }

    }
}
