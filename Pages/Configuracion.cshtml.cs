using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FerroliTime.Terminal.Services;

namespace FerroliTime.Terminal.Pages;

/// <summary>
/// Edición de appsettings.json del terminal. Sin login, como el resto del terminal: solo entra el
/// equipo cuya IP es el terminal personal de un empleado activo con telefonos_aut.config = 1.
/// </summary>
public class ConfiguracionModel(AccesoTerminal acceso, ConfiguracionService configuracion, IWebHostEnvironment env)
    : PageModel
{
    /// <summary>Filas vacías que se ofrecen para añadir localizaciones nuevas.</summary>
    private const int FilasNuevas = 3;

    [BindProperty]
    public AjustesTerminal Datos { get; set; } = new();

    public Empleado? Configurador { get; private set; }
    public string Ip { get; private set; } = "";
    public bool EsDesarrollo => env.IsDevelopment();
    public string? Error { get; private set; }
    public string? Exito { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await ComprobarAccesoAsync()) return SinAcceso();
        Datos = configuracion.Leer();
        return Pagina();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await ComprobarAccesoAsync()) return SinAcceso();

        Error = Validar();
        if (Error != null) return Pagina();

        // No se guarda una cadena de conexión que no funciona: el terminal dejaría de responder
        // y ya no se podría volver a entrar aquí para corregirla.
        var errorBd = await ConfiguracionService.ProbarConexionAsync(configuracion.CadenaConexion(Datos));
        if (errorBd != null)
        {
            Error = "No se ha guardado: no se puede conectar con la base de datos con esos datos. " + errorBd;
            return Pagina();
        }

        try
        {
            await configuracion.GuardarAsync(Datos, $"{Configurador!.MP_CODI} ({Configurador.Descripcion?.Trim()}) desde {Ip}");
        }
        catch (Exception ex)
        {
            Error = "No se ha podido escribir appsettings.json: " + ex.Message;
            return Pagina();
        }

        // Vuelve a la pantalla de fichaje con la ventana de resultado (se cierra sola, como la de un fichaje).
        TempData["ResultadoTipo"] = "exito";
        TempData["ResultadoTitulo"] = "Configuración guardada";
        TempData["ResultadoTexto"] = "Configuración guardada y aplicada. Se ha dejado copia de la anterior en appsettings.json.bak.";
        return RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnPostProbarBdAsync()
    {
        if (!await ComprobarAccesoAsync()) return SinAcceso();

        var error = await ConfiguracionService.ProbarConexionAsync(configuracion.CadenaConexion(Datos));
        if (error != null) Error = "Conexión fallida: " + error;
        else Exito = "Conexión correcta con la base de datos (no se ha guardado nada; para guardar, vuelva a escribir las contraseñas y pulse Guardar).";
        return Pagina();
    }

    private async Task<bool> ComprobarAccesoAsync()
    {
        Ip = acceso.IpCliente();
        Configurador = await acceso.ConfiguradorAsync();
        return Configurador != null;
    }

    private PageResult SinAcceso()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Page();
    }

    /// <summary>Vuelve a pintar la página sin devolver la contraseña tecleada y con filas para añadir.</summary>
    private PageResult Pagina()
    {
        Datos.ClaveBd = null;
        Datos.Localizaciones = Datos.Localizaciones
            .Where(l => !string.IsNullOrWhiteSpace(l.Prefijo) || !string.IsNullOrWhiteSpace(l.Texto))
            .Concat(Enumerable.Range(0, FilasNuevas).Select(_ => new Localizacion()))
            .ToList();
        return Page();
    }

    private string? Validar()
    {
        // Las filas con prefijo y texto vacíos se descartan (así se borra una localización).
        Datos.Localizaciones = Datos.Localizaciones
            .Select(l => new Localizacion { Prefijo = l.Prefijo?.Trim() ?? "", Texto = l.Texto?.Trim() ?? "" })
            .Where(l => l.Prefijo != "" || l.Texto != "")
            .ToList();

        if (string.IsNullOrWhiteSpace(Datos.Servidor) || string.IsNullOrWhiteSpace(Datos.BaseDatos))
            return "El servidor y la base de datos son obligatorios.";
        if (string.IsNullOrWhiteSpace(Datos.Nombre))
            return "El nombre de la aplicación es obligatorio.";
        if (Datos.MinutosEntreMarcajes is < 0 or > 120)
            return "Minutos entre marcajes: entre 0 y 120.";
        if (Datos.SegundosMensaje is < 3 or > 120)
            return "Segundos del mensaje: entre 3 y 120.";
        if (string.IsNullOrWhiteSpace(Datos.LocalizacionDefecto))
            return "La localización por defecto es obligatoria.";
        if (Datos.LocalizacionDefecto.Trim().Length > 256 || Datos.Localizaciones.Any(l => l.Texto.Length > 256))
            return "Las localizaciones no pueden pasar de 256 caracteres (tamaño de DESC_LOCA en Marcajes).";
        if (Datos.Localizaciones.Any(l => l.Prefijo == "" || l.Texto == ""))
            return "Cada localización necesita prefijo de IP y texto (deje las dos vacías para quitarla).";
        if (Datos.Localizaciones.GroupBy(l => l.Prefijo).Any(g => g.Count() > 1))
            return "Hay prefijos de IP repetidos en las localizaciones.";
        return null;
    }
}
