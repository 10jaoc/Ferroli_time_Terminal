using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using FerroliTime.Terminal.Services;

namespace FerroliTime.Terminal.Pages;

/// <summary>
/// Pantalla única del terminal (terminal.asp):
/// 1. Se comprueba la IP (PC común o terminal personal) y que la oficina esté abierta.
/// 2. PC común: se teclea el nº de empleado. Terminal personal: el empleado es el de la IP.
/// 3. Se muestra ENTRADA o SALIDA según su último marcaje de hoy y los marcajes del día.
/// </summary>
public class IndexModel(AccesoTerminal acceso, TerminalRepository repo, IOptionsMonitor<TerminalOptions> opciones) : PageModel
{
    public Acceso Terminal { get; private set; } = new("", TipoTerminal.NoAutorizado, null);
    public string? MotivoCerrado { get; private set; }
    public Empleado? Empleado { get; private set; }
    public List<MarcajeDia> Marcajes { get; private set; } = [];
    public bool TocaEntrada { get; private set; } = true;
    public string? Error { get; private set; }
    public string? CodigoTecleado { get; private set; }
    public DateTime Ahora { get; } = DateTime.Now;
    public int SegundosInactividad => opciones.CurrentValue.SegundosInactividad;

    public bool Abierto => Terminal.Tipo != TipoTerminal.NoAutorizado && MotivoCerrado == null;

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await PrepararAsync()) return Pagina();
        if (Terminal.Tipo == TipoTerminal.Personal) await CargarEmpleadoAsync(Terminal.Empleado!);
        return Page();
    }

    /// <summary>PC común: el empleado teclea su número.</summary>
    public async Task<IActionResult> OnPostIdentificarAsync(string? codigo)
    {
        if (!await PrepararAsync()) return Pagina();
        if (Terminal.Tipo == TipoTerminal.Personal) return RedirectToPage();

        await CargarEmpleadoAsync(codigo);
        return Page();
    }

    public async Task<IActionResult> OnPostFicharAsync(string? codigo)
    {
        if (!await PrepararAsync()) return Pagina();

        // En un terminal personal manda la IP, no lo que venga en el formulario.
        if (!await CargarEmpleadoAsync(Terminal.Tipo == TipoTerminal.Personal ? Terminal.Empleado! : codigo))
            return Page();

        var ahora = DateTime.Now;
        var cfg = opciones.CurrentValue;
        var grabado = await repo.FicharAsync(Empleado!.MP_CODI, TocaEntrada, ahora, cfg.MinutosEntreMarcajes,
            Terminal.Ip, cfg.LocalizacionDe(Terminal.Ip));

        TempData["Mensaje"] = grabado
            ? $"{(TocaEntrada ? "ENTRADA" : "SALIDA")} registrada a las {ahora:HH:mm:ss} · {Empleado.Descripcion?.Trim()}"
            : $"Ya tiene un marcaje en los últimos {cfg.MinutosEntreMarcajes} minutos: no se ha registrado otro.";
        TempData["MensajeOk"] = grabado;
        return RedirectToPage();
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
        codigo = codigo?.Trim() ?? "";
        CodigoTecleado = codigo;
        if (codigo == "")
        {
            Error = "Teclee su número de empleado.";
            return false;
        }
        // Nº de empleado de 8 dígitos: los numéricos se completan con ceros a la izquierda.
        if (codigo.Length < 8 && codigo.All(char.IsAsciiDigit)) codigo = codigo.PadLeft(8, '0');

        Empleado = await repo.EmpleadoAsync(codigo, soloReloj99: Terminal.Tipo == TipoTerminal.PcComun);
        if (Empleado is null)
        {
            Error = "Código de empleado no existe. Inténtelo de nuevo.";
            return false;
        }

        Marcajes = await repo.MarcajesDelDiaAsync(Empleado.MP_CODI, Ahora);
        TocaEntrada = TerminalRepository.TocaEntrada(Marcajes);
        return true;
    }
}
