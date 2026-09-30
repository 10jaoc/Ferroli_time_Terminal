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
    hora.textContent = dos(d.getHours()) + ':' + dos(d.getMinutes()) + ':' + dos(d.getSeconds());
    fecha.textContent = dos(d.getDate()) + '/' + dos(d.getMonth() + 1) + '/' + d.getFullYear();
  };
  pintar();
  setInterval(pintar, 1000);
})();

(function () {
  // El aviso de "ENTRADA registrada..." desaparece solo a los pocos segundos.
  var aviso = document.querySelector('[data-ocultar]');
  if (!aviso) return;
  setTimeout(function () { aviso.remove(); }, (parseInt(aviso.getAttribute('data-ocultar'), 10) || 6) * 1000);
})();

(function () {
  // PC común: si el empleado identificado no ficha, se vuelve a pedir el número
  // para que el siguiente no fiche con los datos del anterior.
  var form = document.querySelector('form[data-volver]');
  if (!form) return;

  // Evita el doble clic: un solo envío por pulsación.
  form.addEventListener('submit', function () {
    var b = form.querySelector('button[type=submit]');
    if (b) b.disabled = true;
  });

  var segundos = parseInt(form.getAttribute('data-volver'), 10);
  if (segundos) setTimeout(function () { window.location.href = window.location.pathname; }, segundos * 1000);
})();
