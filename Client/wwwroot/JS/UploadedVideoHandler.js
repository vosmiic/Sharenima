var dotNetHelper;

function setDotNetHelper(value) {
    dotNetHelper = value;
}

let video;
let lastExecution = new Date();
let videoPlaying;

function loadVideoFunctions(autoplay) {
    video = document.querySelector('#mediaPlayer');

    video.addEventListener('play', function () {
        videoPlaying = true;
        uploadedVideoStateChange(1);
    });

    video.addEventListener('pause', function () {
        videoPlaying = false;
        if (video.readyState === 4) {
            console.log("pause")
            uploadedVideoStateChange(2);
        }
    });
    
    video.addEventListener('ended', function () {
        videoPlaying = false;
        uploadedVideoStateChange(0)
    });
    
    video.addEventListener('seeked', function () {
        if (new Date() - lastExecution >= 100) {
            updateBackendProgress();
        }
    });
    
    video.addEventListener('loadedmetadata', function () {
        dotNetHelper.invokeMethodAsync('RequestStoredVideoTime').then((time) => {
            video.currentTime = time;
        });
    });
    
    if (autoplay) {
        video.play().catch(() => {
            video.muted = true;
            video.play();
        })
    }
}

function uploadedVideoStateChange(state) {
    dotNetHelper.invokeMethodAsync('StateChange', state);
}

function updateBackendProgress() {
    dotNetHelper.invokeMethodAsync('ProgressChange', video.currentTime, true);
    lastExecution = new Date();
}

function playFileUpload() {
    if (video.paused && !videoPlaying) {
        video.play().catch(() => {
            video.muted = true;
            video.play();
        });
    }
}

function pauseFileUpload() {
    if (!video.paused && videoPlaying) {
        video.pause();
    }
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
    if (typeof video !== 'undefined' && video !== 'undefined' && !video.ended) {
        var currentTime = video.currentTime;
        if (currentTime) {
            dotNetHelper.invokeMethodAsync('ProgressChange', currentTime, false);
        }
    }
}, 500)