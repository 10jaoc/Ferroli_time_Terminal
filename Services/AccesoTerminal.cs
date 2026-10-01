using System.Net;
using Microsoft.Extensions.Options;

namespace FerroliTime.Terminal.Services;

public enum TipoTerminal
{
    /// <summary>La IP no está en telefonos_aut ni en los parámetros activos 500 a 599.</summary>
    NoAutorizado,
    /// <summary>PC común (parámetros activos 500 a 599): se teclea el nº de empleado.</summary>
    PcComun,
    /// <summary>Terminal personal (telefonos_aut.Id_telefono): el empleado es fijo.</summary>
    Personal,
}

public record Acceso(string Ip, TipoTerminal Tipo, string? Empleado);

/// <summary>
/// Sin login: el acceso lo da la IP del equipo que llama, como en terminal.asp.
/// Primero se mira si es un PC común y, si no, si es el terminal de algún empleado.
/// </summary>
public class AccesoTerminal(
    TerminalRepository repo, IHttpContextAccessor http, IWebHostEnvironment env, IOptionsMonitor<TerminalOptions> opciones)
{
    public string IpCliente()
    {
        var simulada = opciones.CurrentValue.IpSimulada;
        if (env.IsDevelopment() && !string.IsNullOrWhiteSpace(simulada)) return simulada.Trim();

        var ip = http.HttpContext?.Connection.RemoteIpAddress;
        if (ip is null) return "";
        if (IPAddress.IsLoopback(ip)) return "127.0.0.1";
        return (ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip).ToString();
    }

    /// <summary>
    /// Empleado que puede entrar en la configuración del terminal desde este equipo: el de la IP,
    /// activo y con config = 1. Null si no hay ninguno.
    /// </summary>
    public async Task<Empleado?> ConfiguradorAsync()
    {
        var ip = IpCliente();
        return ip == "" ? null : await repo.ConfiguradorDeTerminalAsync(ip);
    }

    /// <summary>
    /// Para la cabecera (rueda de configuración): si falla la base de datos devuelve false en vez
    /// de romper la página, que puede ser la de error.
    /// </summary>
    public async Task<bool> PuedeConfigurarAsync()
    {
        try { return await ConfiguradorAsync() != null; }
        catch { return false; }
    }

    public async Task<Acceso> ComprobarAsync()
    {
        var ip = IpCliente();
        if (ip == "") return new Acceso(ip, TipoTerminal.NoAutorizado, null);
        if (await repo.EsPcComunAsync(ip)) return new Acceso(ip, TipoTerminal.PcComun, null);

        var empleado = await repo.EmpleadoDeTerminalAsync(ip);
        return empleado != null
            ? new Acceso(ip, TipoTerminal.Personal, empleado)
            : new Acceso(ip, TipoTerminal.NoAutorizado, null);
    }
}
