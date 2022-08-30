var dotNetHelper;

function setDotNetHelper(value) {
    dotNetHelper = value;
}

let video;
let lastExecution = new Date();

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
    
    video.addEventListener('seeked', function () {
        if (new Date() - lastExecution >= 100) {
            console.log("seeked");
            updateBackendProgress();
        }
    });
    
    video.addEventListener('loadedmetadata', function () {
        dotNetHelper.invokeMethodAsync('RequestStoredVideoTime').then((time) => {
            video.currentTime = time;
        });
    });
}

function uploadedVideoStateChange(state) {
    dotNetHelper.invokeMethodAsync('StateChange', state);
}

function updateBackendProgress() {
    dotNetHelper.invokeMethodAsync('ProgressChange', video.currentTime);
    lastExecution = new Date();
}

function playFileUpload() {
    video.play();
}

function pauseFileUpload() {
    video.pause();
}

function setCurrentUploadedVideoTime(time) {
    let success = true;
    try {
        video.currentTime = time;
    } catch (e) {
        success = false;
    }
    return success;
}

setInterval(function () {
    if (typeof video !== 'undefined' && video !== 'undefined') {
        var currentTime = video.currentTime;
        if (currentTime) {
            dotNetHelper.invokeMethodAsync('ProgressChange', currentTime);
        }
    }
}, 500)