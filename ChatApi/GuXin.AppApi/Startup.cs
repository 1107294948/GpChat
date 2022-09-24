using Autofac;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GuXin.Core.Configuration;
using GuXin.Core.Extensions;
using GuXin.Core.Filters;
using GuXin.Core.Middleware;
using GuXin.Core.ObjectActionValidator;
using Newtonsoft.Json;

namespace GuXin.App
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        private IServiceCollection Services { get; set; }
        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            //��ʼ��ģ����֤����
            services.UseMethodsModelParameters().UseMethodsGeneralParameters();
            services.AddSingleton<IObjectModelValidator>(new NullObjectModelValidator());
            Services = services;

            services.AddSession();
            services.AddMemoryCache();

            services.AddHttpContextAccessor();
            services.AddMvc(options =>
            {
                options.Filters.Add(typeof(AppApiAuthorizeFilter));
                options.Filters.Add(typeof(ActionExecuteFilter));
                //options.SuppressAsyncSuffixInActionNames = false;
            });
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddControllers(opt =>
            {
                opt.UseCentralRoutePrefix(new RouteAttribute("AppApi/V1"));
            })
             .AddNewtonsoftJson(op =>
             {
                 op.SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver();
                 op.SerializerSettings.DateFormatString = "yyyy-MM-dd HH:mm:ss";
             });
            Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    SaveSigninToken = true,//����token,��̨��֤token�Ƿ���Ч(��Ҫ)
                    ValidateIssuer = true,//�Ƿ���֤Issuer
                    ValidateAudience = true,//�Ƿ���֤Audience
                    ValidateLifetime = true,//�Ƿ���֤ʧЧʱ��
                    ValidateIssuerSigningKey = true,//�Ƿ���֤SecurityKey
                    ValidAudience = AppSetting.Secret.Audience,//Audience
                    ValidIssuer = AppSetting.Secret.Issuer,//Issuer���������ǰ��ǩ��jwt������һ��
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AppSetting.Secret.JWT))
                };
                options.Events = new JwtBearerEvents()
                {
                    OnChallenge = context =>
                    {
                        context.HandleResponse();
                        context.Response.Clear();
                        context.Response.ContentType = "application/json";
                        context.Response.StatusCode = 401;
                        context.Response.WriteAsync(new { message = "��Ȩδͨ��", status = false, code = 401 }.Serialize());
                        return Task.CompletedTask;
                    }
                };
            });

            //����appsettings.json������
            string corsUrls = Configuration["CorsUrls"];
            if (string.IsNullOrEmpty(corsUrls))
            {
                throw new Exception("�����ÿ������ǰ��Url");
            }
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(
                    builder =>
                    {
                        builder.WithOrigins(corsUrls.Split(","))
                        //���Ԥ���������ʱ��
                         .SetPreflightMaxAge(TimeSpan.FromSeconds(2520))
                        .AllowCredentials()
                        .AllowAnyHeader().AllowAnyMethod();
                    });
            });

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "����App�ӿ��ĵ�", Version = "v1" });
                var security = new Dictionary<string, IEnumerable<string>>
                { { AppSetting.Secret.Issuer, new string[] { } }};
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
                {
                    Description = "JWT��Ȩtokenǰ����Ҫ�����ֶ�Bearer��һ���ո�,��Bearer token",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    BearerFormat = "JWT",
                    Scheme = "Bearer"
                });
                c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "GuXin.Entity.XML"), true);
                c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "GuXin.AppApi.XML"), true);
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            })
             .AddControllers()
            .ConfigureApiBehaviorOptions(options =>
            {
                options.SuppressConsumesConstraintForFormFileParameters = true;
                options.SuppressInferBindingSourcesForParameters = true;
                options.SuppressModelStateInvalidFilter = true;
                options.SuppressMapClientErrors = true;
                options.ClientErrorMapping[404].Link =
                    "https://*/404";
            });

        }

        public void ConfigureContainer(ContainerBuilder builder)
        {
            Services.AddModule(builder, Configuration);
        }
        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IConfiguration configuration)
        {
            app.UseMiddleware<ExceptionHandlerMiddleWare>();
            app.Use(HttpRequestMiddleware.Context);

            app.UseStaticFiles().UseStaticFiles(new StaticFileOptions
            {
                ServeUnknownFileTypes = true
            });
            app.UseDefaultFiles();
            app.UseStaticFiles(new StaticFileOptions()
            {
                FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), @"Upload")),
                //���÷�������Ŀ¼ʱ�ļ��б���
                RequestPath = "/Upload",
                OnPrepareResponse = (Microsoft.AspNetCore.StaticFiles.StaticFileResponseContext staticFile) =>
                {
                    //�����ڴ˴���ȡ�������Ϣ����Ȩ����֤
                    //  staticFile.File
                    //  staticFile.Context.Response.StatusCode;
                }
            });
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c =>
               {
                   c.SwaggerEndpoint("/swagger/v1/swagger.json", "����APP�ӿ�-v1");
                   c.DocExpansion(DocExpansion.None); //->�޸Ľ����ʱ�Զ��۵�
               }
                );
            }
            //����HttpContext
            app.UseStaticHttpContext();

            app.UseRouting();

            app.UseCors();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                name: "default",
                pattern: "{controller=ApiHome}/{action=Index}/{id?}");
            });



            #region imserverע��
            Console.WriteLine($"{(env.EnvironmentName)}��������,WebApi��ַ:[{configuration["Urls"]}],ImServer��ַ:[{configuration["ImServerUrls"]}]");

            ImHelper.Initialization(new ImClientOptions
            {
                Redis = new FreeRedis.RedisClient(AppSetting.RedisConnectionString),
                Servers = AppSetting.ImServerUrls
            });

            ImHelper.Instance.OnSend += (s, e) => Console.WriteLine($"ImClient.SendMessage(server={e.Server},data={JsonConvert.SerializeObject(e.Message)})");

            ImHelper.EventBus(
                t =>
                {
                    Console.WriteLine(t.clientId + DateTime.Now.ToString() + "������");
                    //var onlineUids = ImHelper.GetClientListByOnline();
                    //ImHelper.SendMessage(t.clientId, onlineUids, $"�û�{t.clientId}������");
                },
                t =>
                {
                    Console.WriteLine(t.clientId + DateTime.Now.ToString() + "������");
                });
            #endregion  
        }
    }
}
