# Ferroli Time · Terminal

Terminal web de marcajes (ASP.NET Core 8 / Razor Pages) del control horario **Ferroli Time**. Sustituye a
`DISCOD\ASP\F_TIMER\terminal.asp` y graba en la misma tabla `Marcajes` de `FERROLI_TIMER`. La gestión
(empleados, terminales, parámetros, consulta de marcajes) está en `DISCOD\Ferroli_time_Backoffice`.

## Acceso: sin login, por IP

Solo se puede fichar desde los equipos cuya IP está dada de alta (desde el backoffice):

| Dónde está la IP | Tipo | Cómo se ficha |
|---|---|---|
| Parámetros **activos 500 a 599** (`CMSParametros.ValorAlfanumerico`) | PC común / tablet de planta | Se **pasa la tarjeta** por el lector USB o por la cámara y se ficha en el acto. Solo empleados activos con **reloj 99**. |
| **Terminales/Empleados** (`telefonos_aut.Id_telefono`), empleado **activo** | Terminal personal | El empleado es el de esa IP: pulsa ENTRADA o SALIDA. |

Cualquier otra IP ve «Terminal no autorizado» (HTTP 403) con su IP, para comunicarla al administrador.

## PC común (lector USB o cámara)

- **Solo con tarjeta**: no hay campo de texto ni botón «Fichar». El número llega por el lector USB o por la
  cámara y se ficha nada más leerlo, sin pulsar ENTRADA/SALIDA.
- **Validación** del número leído contra `telefonos_aut`:

  | Caso | Mensaje |
  |---|---|
  | No existe o está desactivado | «Empleado inexistente (00000122).» |
  | Activo pero su ficha no tiene reloj **99** | «NOMBRE (00000122): usuario no autorizado a usar este tipo de terminal.» |
  | Correcto | «ENTRADA / SALIDA registrada correctamente», con nombre y hora |
  | Ya fichó en los últimos `MinutosEntreMarcajes` | «No se ha registrado» (aviso) |

- **Ventana del resultado**: se cierra sola a los `Terminal:SegundosMensaje` (15 s) o al tocarla; mientras está
  abierta se puede leer la siguiente tarjeta.
- El número leído se limpia (espacios y caracteres de control) y, si es numérico de menos de 8 cifras, se
  completa con ceros a la izquierda.

### Lector USB

Funciona como un teclado: escribe el código y un Intro. Las pulsaciones se recogen en toda la página (no hace
falta que nada tenga el foco), también con la cámara abierta.

- Se distingue de una persona por la velocidad: el lector escribe en milisegundos; si entre dos teclas pasan
  **más de 60 ms** se considera tecleo a mano, no se envía y sale «Use su tarjeta en el lector o en la cámara».
  Es una barrera de la pantalla: quien manipule el navegador podría enviar un número.
- Un Intro doble del lector solo envía una vez.

### Cámara (tablet o PC con webcam)

`wwwroot/js/camara.js`, con la librería **html5-qrcode 2.3.8** servida en `wwwroot/lib/html5-qrcode` (no depende
de internet).

- **Se abre sola** al entrar y tras cada fichaje si el equipo tiene cámara; sin cámara no se muestra nada y se usa
  el lector USB.
- Por defecto la cámara **frontal** (la que mira a quien ficha en una tablet fija).
- `Terminal:SelectorCamara`:
  - **0** (por defecto) — **terminal de planta**: sin desplegable ni botón, cámara **siempre activa**.
  - **1** — se puede elegir la cámara (si hay varias) y apagarla/encenderla; el equipo lo recuerda.
  Para fijar la cámara de una tablet: poner 1, elegirla y volver a 0 (se sigue usando la elegida).
- La **misma tarjeta se ignora 20 s**, para que no fiche dos veces mientras sigue delante de la cámara.
- Formatos: Code 128, **Code 39**, EAN-13/8, UPC-A, ITF y **QR**. Con webcams de PC (enfoque fijo) se leen bien los
  QR y mal los códigos de barras; en Android, Chrome usa su lector nativo y lee mucho mejor los de barras.
- El navegador solo da la cámara en **https** (o `localhost`). La primera vez pide permiso: hay que **permitirlo**
  (si se deniega, se cambia en el candado de la barra de direcciones).
- Preferencias por equipo en `localStorage`: `fertime.camaraId` (cámara elegida), `fertime.camaraApagada`
  (solo con `SelectorCamara = 1`) y `fertime.ultimaLectura` (para ignorar la misma tarjeta).

## Terminal personal

El empleado es el **activo** cuyo `telefonos_aut.Id_telefono` es la IP del equipo (vale cualquier reloj). Ve su
nombre, el botón ENTRADA o SALIDA y sus marcajes del día; el resultado sale en la misma ventana que en el PC común.

## Reglas (las de terminal.asp)

- **Oficina cerrada** si el parámetro **10** tiene *Valor numérico* = 0, o la hora está fuera del horario del
  parámetro **11** (*Numérico desde/hasta*, horas decimales).
- **Fin de semana cerrado** (sábado y domingo). `Terminal:PermitirFinDeSemana` lo habilita.
- **ENTRADA o SALIDA**: si hoy no hay marcajes o el último es una salida → ENTRADA; si no → SALIDA.
  Se decide en el servidor al grabar. En la tabla, `ENTRADA = 0` es entrada y `1` salida.
- **Sin duplicados**: no se graba si el empleado ya fichó en los últimos `Terminal:MinutosEntreMarcajes`
  minutos (5), comprobado en el propio `INSERT`.
- Se graba `MP_FECH` (AAAAMMDD), `MP_HORA` (HHMMSS) con la **hora del servidor**, `Id_telefono` y `CrtUser` =
  IP, `CrtProcess` = `FerroliTime.Terminal`, `Status` = `ACT` y `DESC_LOCA` según `Terminal:Localizaciones`.

Diferencias con la ASP:

- Consultas parametrizadas (la ASP concatenaba la IP y el código en el SQL).
- `terminal.asp` pretendía cerrar sábado y domingo, pero por un error solo cerraba el sábado; aquí cierra ambos.
- Los prefijos de localización de la ASP comparaban 9 caracteres y algunos no coincidían nunca (p. ej.
  `192.168.150` → siempre Burgos); ahora son configurables y se comparan completos (teletrabajo → «TELETRABAJO»).
- PCs comunes: parámetros **500 a 599** y solo los **activos** (la ASP: 500 a 510, sin mirar el estado).
- Terminal personal: solo empleados **activos** (la ASP cogía el primero con esa IP, aunque estuviera de baja).
- PC común: se ficha con la tarjeta (lector o cámara) en un solo paso; en la ASP se tecleaba el número y luego
  se pulsaba ENTRADA/SALIDA.

## Configuración (`appdata\appsettings.json`)

Si hay un `appsettings.json` en la raíz del sitio, la aplicación lo mueve a `appdata` al arrancar.

| Clave | Uso |
|---|---|
| `ConnectionStrings:SqlServer` | Conexión a `FERROLI_TIMER` (la misma que el backoffice). |
| `App:Nombre` | Nombre de la cabecera («Ferroli Time»). |
| `Terminal:MinutosEntreMarcajes` | Ventana anti-duplicados (5). |
| `Terminal:PermitirFinDeSemana` | `false` = no se ficha sábado ni domingo. |
| `Terminal:SegundosMensaje` | PC común: segundos que se ve el resultado del fichaje (15). |
| `Terminal:SelectorCamara` | PC común: `0` = terminal de planta, cámara siempre activa (por defecto); `1` = se puede elegir la cámara y apagarla. |
| `Terminal:Localizaciones` / `LocalizacionDefecto` | Texto de `DESC_LOCA` según el principio de la IP (se usa el primer prefijo que coincida). |
| `Terminal:IpSimulada` | **Solo en Development**: fichar desde el PC de desarrollo como si fuera esa IP. En producción se ignora. |

Una clave que falte toma el valor por defecto indicado. `appdata\appsettings.json` contiene contraseñas y no se
sube a Git ni se copia al publicar; la plantilla es `appsettings.example.json`.

### Página de configuración (rueda ⚙ de la cabecera)

La rueda de la derecha de la cabecera abre `/Configuracion`, que edita `appdata\appsettings.json`: conexión a la base
de datos, nombre, minutos entre marcajes, segundos del mensaje, fin de semana, selector de cámara y
localizaciones. **Solo se ve y solo se puede entrar** desde la IP que es el terminal personal de un empleado
**activo** con `telefonos_aut.config = 1` (se marca en el backoffice: Terminales/Empleados → «¿Acceso a la
configuración del terminal?»). Desde cualquier otro equipo devuelve 403.

- Antes de guardar se prueba la conexión: una cadena que no funciona no se guarda.
- Se deja copia en `appdata\appsettings.json.bak`, se escribe sin dejar el fichero a medias y se aplica sin reiniciar.
- La contraseña de la base de datos nunca se muestra; en blanco = no cambiarla.
- Cada cambio queda en el log con el empleado y la IP.
- La única protección es la IP: marcar `config = 1` solo en equipos de confianza.

## Desarrollo

```powershell
dotnet run          # http://localhost:5081
```

- Desde el propio PC la IP es `127.0.0.1` (no autorizada). Para probar, pon en `Terminal:IpSimulada` una IP de
  `telefonos_aut` (terminal personal) o de los parámetros activos 500–599 (PC común).
- Las vistas Razor van compiladas: un cambio en `.cshtml` o `.cs` solo se ve al **reiniciar** `dotnet run`;
  los de `wwwroot` (JS/CSS) basta con recargar con Ctrl+F5.
- **Cuidado al probar**: la base de datos es la de producción y cada lectura válida graba un marcaje real.
  Para probar sin grabar, usa un número que no exista (p. ej. `99999999`).

## Publicación

`Publicar.cmd` genera `bin\publish` (el servidor necesita el ASP.NET Core 8 Hosting Bundle). Copiar el
contenido sobre la carpeta del sitio con el sitio parado (o con `app_offline.htm`), conservando su
carpeta `appdata` (el zip no la lleva). En el backoffice, **Configuración → URL del terminal** activa el menú
«Emular terminal».

- La cuenta del grupo de aplicaciones de IIS necesita **permiso de lectura y escritura** en la carpeta `appdata` del
  sitio para que la página de configuración pueda guardar `appdata\appsettings.json`.
- **Tablets**: el sitio debe ir por **https** (cámara) y la IP de la tablet tiene que estar en un parámetro
  activo 500–599; con WiFi/DHCP conviene reservarle la IP.
- Si el sitio se publica detrás de un proxy o balanceador, la IP que llega es la del proxy: habría que
  configurar *Forwarded Headers*. En IIS directo (in-process) llega la IP real del equipo.
