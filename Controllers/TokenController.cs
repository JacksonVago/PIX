using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PIX.Models;
using PIX.Repositories;
using PIX.Services;

namespace PIX.Controllers
{
    [Route("api/PIX/[controller]")]
    [ApiController]
    public class TokenController : Controller
    {
        private readonly UsuarioRepository _repository;
        private readonly IHttpContextAccessor _httpContext;

        public TokenController(UsuarioRepository repository, IHttpContextAccessor contextAccessor)
        {
            _repository = repository;
            _httpContext = contextAccessor;

        }

        [HttpPost]
        public async Task<IActionResult> CreateJwtTokenAsync([FromBody] Usuariotoken user)
        {
            Usuario user_ret = new Usuario();
            user_ret = await _repository.ValidaUsuario(user.username, user.password);
            if (user_ret != null)
            {
                if (user_ret.id > 0)
                {
                    try
                    {
                        var token = TokenService.GeraToken(user_ret);
                        if (token != null)
                        {
                            var ip = _httpContext.HttpContext.Connection.LocalIpAddress.ToString();
                            var metodo = _httpContext.HttpContext.Request.Path.ToString();

                            if (await _repository.GravarAcesso(user_ret, token.access_token, ip, metodo, JsonConvert.SerializeObject(user_ret)))
                            {
                                return Ok(new
                                {
                                    token = token

                                });
                            }
                            else
                            {
                                return NotFound("Problemas na gravação do acesso.");
                            }
                        }
                        else
                        {
                            return NotFound("Usuário não cadastrado em nosso sistema");
                        }
                    }
                    catch (Exception ex)
                    {
                        return NotFound(new { message = ex.Message.ToString() });
                    }
                }
                else
                {
                    return BadRequest(user_ret.username);
                }
            }

            return BadRequest("Credenciais de usuário inválida.");
        }

        [HttpGet]
        public async Task<bool> ativaPIX()
        {
            return true;
        }

    }
}
