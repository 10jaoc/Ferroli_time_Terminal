(function () {
  // PC común en tablet: lectura de la tarjeta con la cámara (html5-qrcode, servida en wwwroot/lib).
  // La cámara se abre sola al entrar si el equipo tiene una, salvo que en ese equipo se haya
  // apagado con el botón (se recuerda en localStorage, p. ej. un PC con lector USB).
  // El lector USB y el teclado siguen funcionando igual.
  var form = document.querySelector('form[data-lector]');
  var boton = document.getElementById('btnCamara');
  if (!form || !boton) return;
  if (typeof Html5Qrcode === 'undefined' || !navigator.mediaDevices) { boton.hidden = true; return; }

  var campo = form.querySelector('input[name=codigo]');
  var zona = form.querySelector('[data-camara]');
  var selector = document.getElementById('camaras');
  // Si falta algún elemento (p. ej. una página en caché de otra versión), la cámara funciona igual.
  var titulo = form.querySelector('[data-titulo-lector], label[for=codigo]') || document.createElement('p');
  var avisoCamara = form.querySelector('[data-aviso-camara]') || document.createElement('p');
  var textoLector = titulo.textContent;

  var CLAVE_APAGADA = 'fertime.camaraApagada';
  var CLAVE_ID = 'fertime.camaraId';
  var CLAVE_ULTIMA = 'fertime.ultimaLectura';
  // Tras fichar, la misma tarjeta sigue delante de la cámara: se ignora durante este tiempo.
  var IGNORAR_MS = 20000;
  // Terminal:SelectorCamara = 1 en appsettings.json: se puede elegir la cámara y apagarla/encenderla.
  // Con 0 (terminal de planta) no hay desplegable ni botón: la cámara está siempre activa, en la
  // elegida antes en este equipo (o la frontal).
  var permitirSelector = form.getAttribute('data-selector-camara') === '1';
  if (!permitirSelector) boton.hidden = true;

  var scanner = null;
  var enviado = false;

  function leer(k) { try { return localStorage.getItem(k); } catch (e) { return null; } }
  function guardar(k, v) {
    try { if (v == null) localStorage.removeItem(k); else localStorage.setItem(k, v); } catch (e) { }
  }

  function aviso(texto) {
    avisoCamara.textContent = texto || '';
    avisoCamara.hidden = !texto;
  }

  // Estado visible (el atributo data-camara-activa oculta el icono del lector en el CSS).
  function mostrarActiva(activa) {
    form.toggleAttribute('data-camara-activa', activa);
    zona.hidden = !activa;
    boton.textContent = activa ? 'Apagar la cámara' : '📷 Usar la cámara';
    titulo.textContent = activa ? 'Acerque su tarjeta a la cámara' : textoLector;
  }

  async function cargarCamaras() {
    try {
      var lista = await Html5Qrcode.getCameras();
      var elegida = leer(CLAVE_ID) || '';
      selector.innerHTML = '';
      var auto = document.createElement('option');
      auto.value = '';
      auto.textContent = 'Cámara frontal (automática)';
      selector.appendChild(auto);
      lista.forEach(function (c) {
        var o = document.createElement('option');
        o.value = c.id;
        o.textContent = c.label || c.id;
        selector.appendChild(o);
      });
      selector.value = lista.some(function (c) { return c.id === elegida; }) ? elegida : '';
      selector.hidden = !permitirSelector || lista.length < 2;
      selector.disabled = !permitirSelector;
    } catch (e) { /* sin permiso todavía: se rellena después de abrir la cámara */ }
  }

  // Sin cámara (o sin permiso para usarla en una apertura automática) no se molesta con avisos:
  // el equipo sigue funcionando con el lector USB o el teclado.
  function sinCamara(e) { return /NotFound|DevicesNotFound|NotReadable|TrackStart/i.test(String(e && (e.name || e))); }

  async function iniciar(automatico) {
    if (scanner) return;
    aviso('');
    mostrarActiva(true);
    scanner = new Html5Qrcode('lector', {
      formatsToSupport: [
        Html5QrcodeSupportedFormats.CODE_128, Html5QrcodeSupportedFormats.CODE_39,
        Html5QrcodeSupportedFormats.EAN_13, Html5QrcodeSupportedFormats.EAN_8,
        Html5QrcodeSupportedFormats.UPC_A, Html5QrcodeSupportedFormats.ITF,
        Html5QrcodeSupportedFormats.QR_CODE
      ],
      // Lector nativo del navegador si existe (Chrome Android): lee mucho mejor los códigos 1D.
      experimentalFeatures: { useBarCodeDetectorIfSupported: true },
      verbose: false
    });

    var id = leer(CLAVE_ID);
    // Con videoConstraints la librería ignora el primer parámetro de start(): la cámara va también aquí.
    // Por defecto la frontal: en una tablet fija es la que mira a quien ficha.
    var video = { width: { ideal: 1920 }, height: { ideal: 1080 }, advanced: [{ focusMode: 'continuous' }] };
    if (id) video.deviceId = { exact: id }; else video.facingMode = { ideal: 'user' };

    try {
      await scanner.start(id || { facingMode: 'user' }, {
        fps: 15,
        // Rectángulo ancho y bajo: los códigos de barras son horizontales.
        qrbox: function (w, h) { return { width: Math.floor(w * 0.9), height: Math.floor(Math.min(h * 0.5, w * 0.4)) }; },
        videoConstraints: video
      }, alLeer);
      cargarCamaras();
    } catch (e) {
      scanner = null;
      mostrarActiva(false);
      if (id) {
        // La cámara elegida ya no existe: se vuelve a la automática.
        guardar(CLAVE_ID, null);
        return iniciar(automatico);
      }
      if (automatico && sinCamara(e)) { boton.hidden = true; return; }
      aviso('No se ha podido abrir la cámara. ' + (location.protocol !== 'https:' && location.hostname !== 'localhost'
        ? 'El navegador solo permite la cámara en páginas https.'
        : 'Compruebe que el navegador tiene permiso para usarla.'));
    }
  }

  async function detener() {
    if (!scanner) return;
    try { await scanner.stop(); scanner.clear(); } catch (e) { }
    scanner = null;
    mostrarActiva(false);
  }

  function alLeer(texto) {
    texto = (texto || '').trim();
    if (!texto || enviado) return;

    var ultima = null;
    try { ultima = JSON.parse(leer(CLAVE_ULTIMA) || 'null'); } catch (e) { }
    if (ultima && ultima.codigo === texto && Date.now() - ultima.hora < IGNORAR_MS) return;

    enviado = true;
    guardar(CLAVE_ULTIMA, JSON.stringify({ codigo: texto, hora: Date.now() }));
    if (navigator.vibrate) navigator.vibrate(80);
    campo.value = texto;
    // requestSubmit pasa por el evento submit (y su protección contra envíos dobles de terminal.js).
    if (form.requestSubmit) form.requestSubmit(); else form.submit();
  }

  boton.addEventListener('click', function () {
    if (!permitirSelector) return;
    boton.blur(); // que el Intro del lector USB no vuelva a pulsar el botón
    if (scanner) { guardar(CLAVE_APAGADA, '1'); detener(); }
    else { guardar(CLAVE_APAGADA, null); iniciar(false); }
  });

  selector.addEventListener('change', async function () {
    if (!permitirSelector) return;
    guardar(CLAVE_ID, selector.value || null);
    selector.blur();
    await detener();
    iniciar(false);
  });

  // Apertura automática si el equipo tiene cámara. Solo se respeta que se apagara aquí con el botón
  // cuando el botón está permitido; en un terminal de planta se abre siempre.
  if (!permitirSelector || leer(CLAVE_APAGADA) !== '1') {
    form.setAttribute('data-camara-activa', '');
    navigator.mediaDevices.enumerateDevices()
      .then(function (d) { return d.some(function (x) { return x.kind === 'videoinput'; }); })
      .catch(function () { return true; })
      .then(function (hay) {
        if (hay) iniciar(true);
        else { form.removeAttribute('data-camara-activa'); boton.hidden = true; }
      });
  }
})();
