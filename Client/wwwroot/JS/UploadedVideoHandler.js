var dotNetHelper;

function setDotNetHelper(value) {
    dotNetHelper = value;
}

let uploadedPlayer;
let lastExecution = new Date();
let videoId;
let ready = false;
let lastUpdate = 0;

function loadVideoFunctions(autoplay, currentVideoId, queue) {
    uploadedPlayer = OvenPlayer.create('mediaPlayer', queue);
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
        var playerVolume = getPlayerVolumeCookie();
        if (playerVolume != null) {
            uploadedPlayer.setVolume(playerVolume);
        }
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
        if (ready && uploadedPlayer.getState() !== "paused" && (difference >= 1 || difference <= -1)) {
            lastUpdate = event.position;
            updateServerTime(event.position);
        }
    });
    
    uploadedPlayer.on('volumeChanged', function (event) {
        setPlayerVolumeCookie(event);
    })
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

function changeUploadedVideoSource(queue, autoPlay) {
    uploadedPlayer.pause();
    uploadedPlayer.load(queue);
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
    }, 500);
}

function uploadGetTime() {
    return uploadedPlayer.getPosition();
}

function uploadCheckIfPlayerExists() {
    try {
        uploadedPlayer.getVersion();
        return true;
    } catch (_) {
        return false;
    }
}