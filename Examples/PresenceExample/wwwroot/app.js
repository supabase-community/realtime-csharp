(function () {
    let throttlePause;
    const throttle = (callback, time) => {
        if (throttlePause) return;
        throttlePause = true;

        setTimeout(() => {
            callback();
            throttlePause = false;
        }, time);
    };

    window.registerMouseMoveListener = (dotnetHelper) => {
        document.addEventListener('mousemove', (ev) => {
            throttle(() => {
                dotnetHelper.invokeMethodAsync('HandleMouseMoved', [ev.clientX, ev.clientY]);
            }, 50)
        })
    }
})()