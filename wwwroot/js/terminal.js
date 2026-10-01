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
    var campo = document.querySelector('form[data-lector]:not([data-camara-activa]) input[name=codigo]');
    if (campo) campo.focus();
  }
  ventana.addEventListener('click', cerrar);
})();

(function () {
  // PC común: el lector de códigos de barras funciona como un teclado (número + Intro).
  // El campo recupera siempre el foco, también con la ventana del resultado abierta,
  // para que la siguiente lectura no se pierda. Con la cámara activa (tablet, camara.js) no:
  // abriría el teclado de pantalla todo el rato.
  var form = document.querySelector('form[data-lector]');
  if (!form) return;
  var campo = form.querySelector('input[name=codigo]');
  var enviando = false;

  form.addEventListener('submit', function (e) {
    // Una lectura = un envío (algunos lectores mandan el Intro dos veces).
    if (enviando || !campo.value.trim()) { e.preventDefault(); return; }
    enviando = true;
    campo.readOnly = true;
  });

  var enfocar = function () {
    if (form.hasAttribute('data-camara-activa')) return;
    if (document.activeElement !== campo && !(document.activeElement && document.activeElement.closest('[data-camara]')))
      campo.focus();
  };
  campo.addEventListener('blur', function () { setTimeout(enfocar, 150); });
  enfocar();
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
