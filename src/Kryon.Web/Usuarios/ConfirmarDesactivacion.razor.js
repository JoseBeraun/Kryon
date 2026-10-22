// Abre el <dialog> de confirmación como modal: el resto de la página queda inerte y el navegador pone el foco en el
// botón con autofocus ("Cancelar"). Escape dispara el evento "cancel", que el componente trata como Cancelar.
export function abrirModal(dialogo) {
    if (dialogo && !dialogo.open) {
        dialogo.showModal();
    }
}
