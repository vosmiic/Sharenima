var dotNetHelper;

function setDotNetHelper(value) {
    dotNetHelper = value;
}

let uploadedPlayer;
let lastExecution = new Date();
let videoId;
let ready = false;
let lastUpdate = 0;

function loadVideoFunctions(autoplay, currentVideoId, type, url) {
    uploadedPlayer = OvenPlayer.create('mediaPlayer', getSource(type, url));
    videoId = currentVideoId;

    uploadedPlayer.on('stateChanged', function (event) {
        switch (event.newstate) {
            case "playing":
                onPlay();
                break;
            case "paused":
                onPause();
                break;
            case "complete":
                onComplete();
                break;
                
        } 
    })
    
    let seekPosition;
    uploadedPlayer.on('seek', function (event) {
        seekPosition = event.position;
    })
    uploadedPlayer.on('seeked', function () {
        if (new Date() - lastExecution >= 100) {
            updateBackendProgress(seekPosition);
        }
    });
    
    uploadedPlayer.on('ready', function () {
        dotNetHelper.invokeMethodAsync('RequestInitialVideoTime').then((time) => {
            uploadedPlayer.seek(time);
        });
        if (autoplay) {
            playFileUpload();
        }
        dotNetHelper.invokeMethodAsync('SetReady', true);
        ready = true;
    });
    
    uploadedPlayer.on('time', function (event) {
        let difference = lastUpdate - event.position;
        if (ready && uploadedPlayer.getState() !== "paused" && (difference >= 0.5 || difference <= -0.5)) {
            lastUpdate = event.position;
            updateServerTime(event.position);
        }
    });
}

function onPlay() {
    uploadedVideoStateChange(1);
}

function onPause() {
    uploadedVideoStateChange(2);
}

function onComplete() {
    uploadedVideoStateChange(0);
    ready = false;
    lastUpdate = 0;
}

function uploadedVideoStateChange(state) {
    dotNetHelper.invokeMethodAsync('StateChange', state, videoId);
}

function updateBackendProgress(time) {
    dotNetHelper.invokeMethodAsync('ProgressChange', videoId, time, true);
    lastExecution = new Date();
}

function playFileUpload() {
    if (uploadedPlayer.getState() === "paused" || uploadedPlayer.getState() === "idle") {
        playVideo();
    }
}

function pauseFileUpload() {
    if (uploadedPlayer.getState() !== "paused" || uploadedPlayer.getState() !== "idle") {
        uploadedPlayer.pause();
    }
}

function setCurrentUploadedVideoTime(time, currentVideoId) {
    if (currentVideoId === videoId) {
        let success = true;
        try {
            uploadedPlayer.seek(time);
        } catch (e) {
            success = false;
        }
        return success;
    }
    return false;
}

function destroyVideoElement() {
    document.getElementById("fileUploadContainer").textContent = '';
}

function changeUploadedVideoSource(newSource, type, autoPlay) {
    uploadedPlayer.pause();
    uploadedPlayer.load(getSource(type, newSource));
    if (autoPlay) {
        playFileUpload();
    }
}

function updateServerTime(time) {
    if (uploadedPlayer.getState() !== "complete" && typeof videoId === 'string') {
        dotNetHelper.invokeMethodAsync('ProgressChange', videoId, time, false);
    }
}

function getSource(type, url) {
    return {
        sources: [{
            label: type.startsWith("video") ? "video" : "audio",
            type: type,
            file: url
        }]
    }
}

function playVideo() {
    uploadedPlayer.play();
    let checkIfPLayingInterval = setInterval(function () {
        if (uploadedPlayer.getState() === "paused" || uploadedPlayer.getState() === "idle") {
            uploadedPlayer.setMute(true);
            uploadedPlayer.play();
        } else {
            clearInterval(checkIfPLayingInterval);
        }
    }, 50);
}

function uploadGetTime() {
    return uploadedPlayer.getPosition();
}