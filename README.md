# Ferroli Time · Terminal

Terminal web de marcajes (ASP.NET Core 8 / Razor Pages) del control horario **Ferroli Time**. Sustituye a
`DISCOD\ASP\F_TIMER\terminal.asp` y graba en la misma tabla `Marcajes` de `FERROLI_TIMER`. La gestión
(empleados, terminales, parámetros, consulta de marcajes) está en `DISCOD\Ferroli_time_Backoffice`.

## Acceso: sin login, por IP

Solo se puede fichar desde los equipos cuya IP está dada de alta (desde el backoffice):

| Dónde está la IP | Tipo | Cómo se ficha |
|---|---|---|
| Parámetros **activos 500 a 599** (`CMSParametros.ValorAlfanumerico`) | PC común | Se **pasa la tarjeta por el lector** de códigos de barras (o se teclea el nº y se pulsa Intro) y se ficha en el acto. Solo empleados activos con **reloj 99**. |
| **Terminales/Empleados** (`telefonos_aut.Id_telefono`) | Terminal personal | El empleado es el de esa IP; no se teclea nada. |

Cualquier otra IP ve «Terminal no autorizado» (HTTP 403) con su IP, para comunicarla al administrador.

## Reglas (las de terminal.asp)

- **Oficina cerrada** si el parámetro **10** tiene *Valor numérico* = 0, o la hora está fuera del horario del
  parámetro **11** (*Numérico desde/hasta*, horas decimales).
- **Fin de semana cerrado** (sábado y domingo). `Terminal:PermitirFinDeSemana` lo habilita.
- **ENTRADA o SALIDA**: si hoy no hay marcajes o el último es una salida → ENTRADA; si no → SALIDA.
  Se decide en el servidor al grabar. En la tabla, `ENTRADA = 0` es entrada y `1` salida.
- **Sin duplicados**: no se graba si el empleado ya fichó en los últimos `Terminal:MinutosEntreMarcajes`
  minutos (5), comprobado en el propio `INSERT`.
- **Nº de empleado** de 8 dígitos: los numéricos se completan con ceros a la izquierda.
- Se graba `MP_FECH` (AAAAMMDD), `MP_HORA` (HHMMSS) con la **hora del servidor**, `Id_telefono` y `CrtUser` =
  IP, `CrtProcess` = `FerroliTime.Terminal`, `Status` = `ACT` y `DESC_LOCA` según `Terminal:Localizaciones`.

Diferencias con la ASP:

- Consultas parametrizadas (la ASP concatenaba la IP y el código en el SQL).
- `terminal.asp` pretendía cerrar sábado y domingo, pero por un error solo cerraba el sábado; aquí cierra ambos.
- Los prefijos de localización de la ASP comparaban 9 caracteres y algunos no coincidían nunca (p. ej.
  `192.168.150` → siempre Burgos); ahora son configurables y se comparan completos (teletrabajo → «TELETRABAJO»).
- En un PC común se ficha al leer la tarjeta, sin pulsar ENTRADA/SALIDA: el lector (USB, como un teclado) escribe
  el nº de empleado y un Intro. El resultado sale en una ventana que se cierra a los `Terminal:SegundosMensaje`
  (15 s); el campo del lector conserva el foco para que la siguiente lectura no se pierda.
- **Tablet: lectura con la cámara** (`wwwroot/js/camara.js`, librería html5-qrcode 2.3.8 servida en
  `wwwroot/lib/html5-qrcode`, sin depender de internet). Se activa con «📷 Usar la cámara» y la tablet lo recuerda
  (localStorage): tras cada fichaje la cámara se vuelve a abrir sola. Por defecto la **frontal** (la que mira a
  quien ficha en una tablet fija); si hay varias, se puede elegir y también se recuerda. La misma tarjeta se
  ignora durante 20 s para que no fiche dos veces mientras sigue delante de la cámara. El navegador solo da la
  cámara en **https** (o `localhost`).

## Configuración (`appsettings.json`)

| Clave | Uso |
|---|---|
| `ConnectionStrings:SqlServer` | Conexión a `FERROLI_TIMER` (la misma que el backoffice). |
| `Terminal:MinutosEntreMarcajes` | Ventana anti-duplicados (5). |
| `Terminal:PermitirFinDeSemana` | `false` = no se ficha sábado ni domingo. |
| `Terminal:SegundosMensaje` | PC común: segundos que se ve el resultado del fichaje (15). |
| `Terminal:Localizaciones` / `LocalizacionDefecto` | Texto de `DESC_LOCA` según el principio de la IP. |
| `Terminal:IpSimulada` | **Solo en Development**: fichar desde el PC de desarrollo como si fuera esa IP. |

### Página de configuración (rueda de la cabecera)

La rueda de la derecha de la cabecera abre `/Configuracion`, que edita `appsettings.json` (conexión a la base
de datos, nombre, minutos entre marcajes, fin de semana, segundos del mensaje y localizaciones). **Solo se ve y solo se
puede entrar** desde la IP que es el terminal personal de un empleado **activo** con `telefonos_aut.config = 1`
(se marca en el backoffice: Terminales/Empleados → «¿Acceso a la configuración del terminal?»). Desde
cualquier otro equipo devuelve 403.

- Antes de guardar se prueba la conexión: una cadena que no funciona no se guarda.
- Se deja copia en `appsettings.json.bak`, se escribe sin dejar el fichero a medias y se aplica sin reiniciar.
- La contraseña de la base de datos nunca se muestra; en blanco = no cambiarla.
- Cada cambio queda en el log con el empleado y la IP.

`appsettings.json` contiene contraseñas y no se sube a Git; la plantilla es `appsettings.example.json`.

## Puesta en marcha

```powershell
dotnet run          # http://localhost:5081
```

Desde el propio PC la IP es `127.0.0.1` (no autorizada): para probar, pon en `Terminal:IpSimulada` una IP de
`telefonos_aut` o de los parámetros 500–599.

Publicación para IIS: `Publicar.cmd` (genera `bin\publish`; el servidor necesita el ASP.NET Core 8 Hosting
Bundle). En el backoffice, **Configuración → URL del terminal** activa el menú «Emular terminal».

> Si el sitio se publica detrás de un proxy o balanceador, la IP que llega es la del proxy: habría que
> configurar *Forwarded Headers*. En IIS directo (in-process) llega la IP real del equipo.
