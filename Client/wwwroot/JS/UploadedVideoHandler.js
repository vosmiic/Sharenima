var dotNetHelper;

function setDotNetHelper(value) {
    dotNetHelper = value;
}

let video;

function loadVideoFunctions() {
    video = document.querySelector('#mediaPlayer');

    video.addEventListener('play', function () {
        uploadedVideoStateChange(1);
    });

    video.addEventListener('pause', function () {
        uploadedVideoStateChange(2);
    });
    
    video.addEventListener('ended', function () {
        uploadedVideoStateChange(0)
    });
}

function uploadedVideoStateChange(state) {
    dotNetHelper.invokeMethodAsync('StateChange', state);
}

function playFileUpload() {
    video.play();
}

function pauseFileUpload() {
    video.pause();
}

function setCurrentTime(time) {
    video.currentTime = time;
}