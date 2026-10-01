using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;

namespace FerroliTime.Terminal.Services;

/// <summary>Valores de appsettings.json editables desde la página Configuración del terminal.</summary>
public class AjustesTerminal
{
    // ConnectionStrings:SqlServer, desglosada
    public string Servidor { get; set; } = "";
    public string BaseDatos { get; set; } = "";
    public string UsuarioBd { get; set; } = "";
    /// <summary>Vacía = conservar la actual.</summary>
    public string? ClaveBd { get; set; }
    public bool Encrypt { get; set; }
    public bool TrustServerCertificate { get; set; } = true;

    // App
    public string Nombre { get; set; } = "";

    // Terminal
    public int MinutosEntreMarcajes { get; set; } = 5;
    public bool PermitirFinDeSemana { get; set; }
    public int SegundosMensaje { get; set; } = 15;
    /// <summary>1 = se puede elegir la cámara en el PC común; 0 = no.</summary>
    public int SelectorCamara { get; set; }
    public string LocalizacionDefecto { get; set; } = "";
    public List<Localizacion> Localizaciones { get; set; } = [];
    /// <summary>Solo se edita en Development (en producción se ignora y se conserva).</summary>
    public string? IpSimulada { get; set; }
}

/// <summary>
/// Lee y guarda appsettings.json. Antes de escribir deja una copia en appsettings.json.bak y
/// después recarga la configuración, de modo que los cambios se aplican sin reiniciar.
/// </summary>
public class ConfiguracionService(IConfiguration config, IWebHostEnvironment env, ILogger<ConfiguracionService> log)
{
    private static readonly SemaphoreSlim Bloqueo = new(1, 1);
    private string Ruta => Path.Combine(env.ContentRootPath, "appsettings.json");

    public AjustesTerminal Leer()
    {
        var cs = new SqlConnectionStringBuilder(config.GetConnectionString("SqlServer") ?? "");
        var t = config.GetSection("Terminal").Get<TerminalOptions>() ?? new TerminalOptions();

        return new AjustesTerminal
        {
            Servidor = cs.DataSource,
            BaseDatos = cs.InitialCatalog,
            UsuarioBd = cs.UserID,
            Encrypt = cs.Encrypt == SqlConnectionEncryptOption.Mandatory || cs.Encrypt == SqlConnectionEncryptOption.Strict,
            TrustServerCertificate = cs.TrustServerCertificate,
            Nombre = config["App:Nombre"] ?? "",
            MinutosEntreMarcajes = t.MinutosEntreMarcajes,
            PermitirFinDeSemana = t.PermitirFinDeSemana,
            SegundosMensaje = t.SegundosMensaje,
            SelectorCamara = t.SelectorCamara == 1 ? 1 : 0,
            LocalizacionDefecto = t.LocalizacionDefecto,
            Localizaciones = t.Localizaciones,
            IpSimulada = t.IpSimulada,
        };
    }

    /// <summary>Cadena de conexión resultante (con la contraseña actual si no se ha tecleado otra).</summary>
    public string CadenaConexion(AjustesTerminal a)
    {
        var cs = new SqlConnectionStringBuilder(config.GetConnectionString("SqlServer") ?? "")
        {
            DataSource = a.Servidor.Trim(),
            InitialCatalog = a.BaseDatos.Trim(),
            UserID = a.UsuarioBd.Trim(),
            Encrypt = a.Encrypt ? SqlConnectionEncryptOption.Mandatory : SqlConnectionEncryptOption.Optional,
            TrustServerCertificate = a.TrustServerCertificate,
        };
        if (!string.IsNullOrEmpty(a.ClaveBd)) cs.Password = a.ClaveBd;
        return cs.ConnectionString;
    }

    public static async Task<string?> ProbarConexionAsync(string cadena)
    {
        try
        {
            var cs = new SqlConnectionStringBuilder(cadena) { ConnectTimeout = 8 };
            await using var conn = new SqlConnection(cs.ConnectionString);
            await conn.OpenAsync();
            return null;
        }
        catch (Exception ex) { return ex.Message; }
    }

    public async Task GuardarAsync(AjustesTerminal a, string quien)
    {
        await Bloqueo.WaitAsync();
        try
        {
            var texto = await File.ReadAllTextAsync(Ruta);
            var raiz = JsonNode.Parse(texto, documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            })?.AsObject() ?? new JsonObject();

            Seccion(raiz, "ConnectionStrings")["SqlServer"] = CadenaConexion(a);
            Seccion(raiz, "App")["Nombre"] = a.Nombre.Trim();

            var t = Seccion(raiz, "Terminal");
            t["MinutosEntreMarcajes"] = a.MinutosEntreMarcajes;
            t["PermitirFinDeSemana"] = a.PermitirFinDeSemana;
            t["SegundosMensaje"] = a.SegundosMensaje;
            t["SelectorCamara"] = a.SelectorCamara == 1 ? 1 : 0;
            t.Remove("SegundosInactividad"); // ya no se usa (el PC común ficha al leer la tarjeta)
            t["LocalizacionDefecto"] = a.LocalizacionDefecto.Trim();
            var lista = new JsonArray();
            foreach (var l in a.Localizaciones)
                lista.Add(new JsonObject { ["Prefijo"] = l.Prefijo.Trim(), ["Texto"] = l.Texto.Trim() });
            t["Localizaciones"] = lista;
            if (env.IsDevelopment()) t["IpSimulada"] = a.IpSimulada?.Trim() ?? "";

            File.Copy(Ruta, Ruta + ".bak", overwrite: true);
            var nuevo = raiz.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });
            // Escritura a un temporal y reemplazo: nunca queda un appsettings.json a medias.
            var tmp = Ruta + ".tmp";
            await File.WriteAllTextAsync(tmp, nuevo);
            File.Move(tmp, Ruta, overwrite: true);

            (config as IConfigurationRoot)?.Reload();
            log.LogWarning("Configuración del terminal (appsettings.json) modificada por {Quien}", quien);
        }
        finally { Bloqueo.Release(); }
    }

    private static JsonObject Seccion(JsonObject raiz, string nombre)
    {
        if (raiz[nombre] is JsonObject o) return o;
        var nueva = new JsonObject();
        raiz[nombre] = nueva;
        return nueva;
    }
}
