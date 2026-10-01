namespace FerroliTime.Terminal.Services;

/// <summary>Sección "Terminal" de appsettings.json.</summary>
public class TerminalOptions
{
    /// <summary>Minutos durante los que no se admite otro marcaje del mismo empleado (terminal.asp: 5).</summary>
    public int MinutosEntreMarcajes { get; set; } = 5;

    /// <summary>terminal.asp no deja fichar sábados ni domingos.</summary>
    public bool PermitirFinDeSemana { get; set; }

    /// <summary>PC común: segundos que se ve la ventana con el resultado del fichaje antes de cerrarse.</summary>
    public int SegundosMensaje { get; set; } = 15;

    /// <summary>Localización (DESC_LOCA) del marcaje según el principio de la IP del terminal.</summary>
    public List<Localizacion> Localizaciones { get; set; } = [];

    /// <summary>Localización si la IP no coincide con ningún prefijo.</summary>
    public string LocalizacionDefecto { get; set; } = "ALCALDE MARTIN COBOS,09007 BURGOS";

    /// <summary>
    /// Solo en Development: IP que se usa en lugar de la real, para probar desde el PC de desarrollo
    /// como si fuera un terminal concreto. Se ignora en producción.
    /// </summary>
    public string? IpSimulada { get; set; }

    public string LocalizacionDe(string ip) =>
        Localizaciones.FirstOrDefault(l => !string.IsNullOrEmpty(l.Prefijo) && ip.StartsWith(l.Prefijo, StringComparison.Ordinal))?.Texto
        ?? LocalizacionDefecto;
}

public class Localizacion
{
    public string Prefijo { get; set; } = "";
    public string Texto { get; set; } = "";
}
