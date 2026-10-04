using FerroliTime.Terminal.Services;

// Terminal de marcajes de Ferroli Time (sustituye a terminal.asp de DISCOD\ASP\F_TIMER).
// No hay login: solo pueden fichar los equipos cuya IP está en telefonos_aut (terminal
// personal) o en los parámetros activos 500 a 599 (PC común). Ver Services/AccesoTerminal.cs.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<TerminalOptions>(builder.Configuration.GetSection("Terminal"));

if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("SqlServer")))
    throw new InvalidOperationException("Falta la cadena de conexión 'SqlServer' en appsettings.json.");

builder.Services.AddScoped(sp =>
    new TerminalRepository(sp.GetRequiredService<IConfiguration>().GetConnectionString("SqlServer")!));
builder.Services.AddScoped<AccesoTerminal>();
builder.Services.AddScoped<ConfiguracionService>();

var app = builder.Build();

// También en desarrollo: la página de error muestra el aviso (p. ej. sin conexión con la base de datos)
// y, solo en desarrollo, debajo el detalle técnico de la excepción.
app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.Run();
