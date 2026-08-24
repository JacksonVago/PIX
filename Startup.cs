using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PIX.Repositories;
using PIX.Services;

namespace PIX
{
    public class Startup
    {
        private readonly ILogger _logger;
        public Startup(IConfiguration configuration, ILogger<Startup> log)
        {
            Configuration = configuration;
            _logger = log;
        }

        public IConfiguration Configuration { get; }
        public object PIXConfigHash { get; private set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var chave = Encoding.ASCII.GetBytes(PixChavehash.chave);

            services.AddScoped<PIXRepository>();
            services.AddTransient<UsuarioRepository>();
            services.AddTransient<DataRepository>();
            services.AddTransient<PIXRepository>();
            services.AddTransient<PIXItau>();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidIssuer = "natividadesolucoes.com.br",
                    ValidAudience = "natividadesolucoes.com.br",
                    IssuerSigningKey = new SymmetricSecurityKey(chave)
                };
            }
            );

            //services.AddHostedService<PedDistanciaPIXHS>();
            services.AddHostedService<PIXHS>();
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

            _logger.LogInformation("Iniciou a API " + DateTime.Now.ToString("G"));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseAuthentication();
            app.UseMvc();
        }
    }
}
