using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using FerroliTime.Terminal.Services;

namespace FerroliTime.Terminal.Pages;

/// <summary>
/// Pantalla única del terminal (terminal.asp):
/// 1. Se comprueba la IP (PC común o terminal personal) y que la oficina esté abierta.
/// 2. PC común: se lee la tarjeta del empleado con el lector de códigos de barras (o se teclea el
///    número y se pulsa Intro) y se ficha en el acto; el resultado se ve unos segundos en una ventana.
/// 3. Terminal personal: el empleado es el de la IP; pulsa ENTRADA o SALIDA y ve sus marcajes del día.
/// </summary>
public class IndexModel(AccesoTerminal acceso, TerminalRepository repo, IOptionsMonitor<TerminalOptions> opciones) : PageModel
{
    public Acceso Terminal { get; private set; } = new("", TipoTerminal.NoAutorizado, null);
    public string? MotivoCerrado { get; private set; }
    public Empleado? Empleado { get; private set; }
    public List<MarcajeDia> Marcajes { get; private set; } = [];
    public bool TocaEntrada { get; private set; } = true;
    public string? Error { get; private set; }
    public DateTime Ahora { get; } = DateTime.Now;
    public int SegundosMensaje => opciones.CurrentValue.SegundosMensaje;
    public bool SelectorCamara => opciones.CurrentValue.SelectorCamara == 1;

    /// <summary>Reloj que deben tener en su ficha los empleados que fichan en un PC común (Reloj Tablet).</summary>
    private const string RelojPcComun = "99";

    public bool Abierto => Terminal.Tipo != TipoTerminal.NoAutorizado && MotivoCerrado == null;

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await PrepararAsync()) return Pagina();
        if (Terminal.Tipo == TipoTerminal.Personal) await CargarEmpleadoAsync(Terminal.Empleado!);
        return Page();
    }

    /// <summary>PC común: lectura de la tarjeta (o nº tecleado + Intro). Ficha sin más pasos.</summary>
    public async Task<IActionResult> OnPostLeerAsync(string? codigo)
    {
        if (!await PrepararAsync()) return Pagina();
        if (Terminal.Tipo != TipoTerminal.PcComun) return RedirectToPage();

        if (await CargarEmpleadoAsync(codigo)) await RegistrarAsync();
        else Resultado("error", "No se ha registrado", Error!);
        return RedirectToPage();
    }

    /// <summary>Terminal personal: botón ENTRADA / SALIDA.</summary>
    public async Task<IActionResult> OnPostFicharAsync()
    {
        if (!await PrepararAsync()) return Pagina();
        if (Terminal.Tipo != TipoTerminal.Personal) return RedirectToPage();

        // En un terminal personal manda la IP, no lo que venga en el formulario.
        if (!await CargarEmpleadoAsync(Terminal.Empleado!)) return Page();
        await RegistrarAsync();
        return RedirectToPage();
    }

    /// <summary>Graba el marcaje del empleado cargado y deja el resultado para la siguiente pantalla.</summary>
    private async Task RegistrarAsync()
    {
        var ahora = DateTime.Now;
        var cfg = opciones.CurrentValue;
        var grabado = await repo.FicharAsync(Empleado!.MP_CODI, TocaEntrada, ahora, cfg.MinutosEntreMarcajes,
            Terminal.Ip, cfg.LocalizacionDe(Terminal.Ip));

        var quien = $"{Empleado.Descripcion?.Trim()} ({Empleado.MP_CODI})";
        if (grabado)
            Resultado(TocaEntrada ? "entrada" : "salida",
                $"{(TocaEntrada ? "ENTRADA" : "SALIDA")} registrada correctamente",
                $"{quien} · {ahora:HH:mm:ss}");
        else
            Resultado("aviso", "No se ha registrado",
                $"{quien} ya tiene un marcaje en los últimos {cfg.MinutosEntreMarcajes} minutos.");
    }

    /// <param name="tipo">entrada, salida, aviso o error (clase CSS de la ventana).</param>
    private void Resultado(string tipo, string titulo, string texto)
    {
        TempData["ResultadoTipo"] = tipo;
        TempData["ResultadoTitulo"] = titulo;
        TempData["ResultadoTexto"] = texto;
    }

    /// <summary>Comprueba IP, día y horario. Devuelve false si no se puede fichar.</summary>
    private async Task<bool> PrepararAsync()
    {
        Terminal = await acceso.ComprobarAsync();
        if (Terminal.Tipo == TipoTerminal.NoAutorizado) return false;

        if (!opciones.CurrentValue.PermitirFinDeSemana && Ahora.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            MotivoCerrado = "No se puede fichar en fin de semana.";
        else if (!await repo.OficinaAbiertaAsync(Ahora))
            MotivoCerrado = "Oficina cerrada o fuera del horario de fichaje.";
        return MotivoCerrado == null;
    }

    private PageResult Pagina()
    {
        if (Terminal.Tipo == TipoTerminal.NoAutorizado) Response.StatusCode = StatusCodes.Status403Forbidden;
        return Page();
    }

    private async Task<bool> CargarEmpleadoAsync(string? codigo)
    {
        // El lector puede añadir espacios o caracteres de control alrededor del número.
        codigo = new string((codigo ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (codigo == "")
        {
            Error = "Lea su tarjeta o teclee su número de empleado.";
            return false;
        }
        // Nº de empleado de 8 dígitos: los numéricos se completan con ceros a la izquierda.
        if (codigo.Length < 8 && codigo.All(char.IsAsciiDigit)) codigo = codigo.PadLeft(8, '0');

        // El empleado tiene que estar en Terminales/Empleados, activo y, en un PC común (lector o
        // cámara), con reloj 99 en su ficha, como en terminal.asp. Desde su terminal personal vale cualquier reloj.
        var ficha = await repo.FichaEmpleadoAsync(codigo);
        // Uno desactivado se trata como inexistente (no se muestra su nombre).
        Error = ficha switch
        {
            null or { Activo: false } => $"Empleado inexistente ({codigo}).",
            _ when Terminal.Tipo == TipoTerminal.PcComun && ficha.Reloj != RelojPcComun =>
                $"{ficha.Nombre}: usuario no autorizado a usar este tipo de terminal.",
            _ => null,
        };
        if (Error != null) return false;
        Empleado = new Empleado(ficha!.MP_CODI, ficha.Descripcion);

        Marcajes = await repo.MarcajesDelDiaAsync(Empleado.MP_CODI, Ahora);
        TocaEntrada = TerminalRepository.TocaEntrada(Marcajes);
        return true;
    }
}
