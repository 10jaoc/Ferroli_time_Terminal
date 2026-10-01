(function () {
  // Reloj con la hora del servidor (la que se graba en el marcaje), no la del equipo.
  var reloj = document.getElementById('reloj');
  if (!reloj) return;
  var desfase = parseInt(reloj.getAttribute('data-servidor'), 10) - Date.now();
  var hora = reloj.querySelector('.hora');
  var fecha = reloj.querySelector('.fecha');
  var dos = function (n) { return (n < 10 ? '0' : '') + n; };

  var pintar = function () {
    var d = new Date(Date.now() + (isNaN(desfase) ? 0 : desfase));
    // Getters UTC: el valor ya es la hora de pared del servidor, sin zona del equipo.
    hora.textContent = dos(d.getUTCHours()) + ':' + dos(d.getUTCMinutes()) + ':' + dos(d.getUTCSeconds());
    fecha.textContent = dos(d.getUTCDate()) + '/' + dos(d.getUTCMonth() + 1) + '/' + d.getUTCFullYear();
  };
  pintar();
  setInterval(pintar, 1000);
})();

(function () {
  // Ventana con el resultado del fichaje: cuenta atrás y se cierra sola (o al tocarla).
  var ventana = document.querySelector('.resultado-fichaje[data-cerrar]');
  if (!ventana) return;
  var quedan = parseInt(ventana.getAttribute('data-cerrar'), 10) || 15;
  var cuenta = ventana.querySelector('[data-cuenta]');
  var reloj = setInterval(function () {
    quedan--;
    if (cuenta) cuenta.textContent = quedan;
    if (quedan <= 0) cerrar();
  }, 1000);
  function cerrar() {
    clearInterval(reloj);
    ventana.remove();
  }
  ventana.addEventListener('click', cerrar);
})();

(function () {
  // PC común: solo se admite la tarjeta (lector USB o cámara), no teclear el número a mano.
  // El lector USB funciona como un teclado pero escribe todo el código en milisegundos y acaba
  // con Intro; una persona tarda mucho más entre tecla y tecla. Lo que no llega a la velocidad
  // de un lector se descarta. Las pulsaciones se recogen en toda la página (no hay campo de texto,
  // así en la tablet no aparece el teclado de pantalla).
  var form = document.querySelector('form[data-lector]');
  if (!form) return;
  var campo = form.querySelector('input[name=codigo]');
  var avisoTeclado = form.querySelector('[data-solo-tarjeta]');
  var MAX_MS_ENTRE_TECLAS = 60;   // lectores USB: 5-30 ms; una persona: más de 100 ms
  var NUEVA_LECTURA_MS = 1000;    // tras una pausa así, empieza otra lectura
  var leido = '', ultima = 0, aMano = false, enviando = false, ocultarAviso = 0;

  form.addEventListener('submit', function (e) {
    // Una lectura = un envío (algunos lectores mandan el Intro dos veces).
    if (enviando || !campo.value.trim()) { e.preventDefault(); return; }
    enviando = true;
  });

  function avisar() {
    avisoTeclado.hidden = false;
    clearTimeout(ocultarAviso);
    ocultarAviso = setTimeout(function () { avisoTeclado.hidden = true; }, 4000);
  }

  document.addEventListener('keydown', function (e) {
    if (e.ctrlKey || e.altKey || e.metaKey) return;
    if (document.activeElement && document.activeElement.matches('select')) return; // selector de cámara
    var ahora = performance.now();

    if (e.key === 'Enter') {
      e.preventDefault(); // tampoco pulsa el botón que tenga el foco (p. ej. «Apagar la cámara»)
      if (leido.length >= 3 && !aMano) {
        campo.value = leido;
        if (form.requestSubmit) form.requestSubmit(); else form.submit();
      } else if (leido) {
        avisar();
      }
      leido = ''; aMano = false;
      return;
    }
    if (e.key.length !== 1) return;
    e.preventDefault();

    if (ahora - ultima > NUEVA_LECTURA_MS) { leido = ''; aMano = false; }
    else if (leido && ahora - ultima > MAX_MS_ENTRE_TECLAS) aMano = true;
    leido += e.key;
    ultima = ahora;
  });
})();

(function () {
  // Terminal personal: evita el doble clic en ENTRADA / SALIDA.
  var form = document.querySelector('form.form-fichar');
  if (!form) return;
  form.addEventListener('submit', function () {
    var b = form.querySelector('button[type=submit]');
    if (b) b.disabled = true;
  });
})();
