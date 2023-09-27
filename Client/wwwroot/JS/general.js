function getPlayerVolumeCookieName() {
    return "playerVolume";
}
function getPlayerVolumeCookie() {
    var cookies = document.cookie.split(';');
    for (var x in cookies) {
        var separatorIndex = cookies[x].indexOf('=');
        var cookieName = cookies[x].substring(0, separatorIndex).replaceAll(' ', '');
        if (cookieName === getPlayerVolumeCookieName()) {
            return cookies[x].substring(separatorIndex + 1, cookies[x].length);
        }
    }
    
    return null;
}

function setPlayerVolumeCookie(volume) {
    var date = new Date();
    date.setMonth(date.getMonth() + 1);
    // store the volume cookie for 30 days
    document.cookie = getPlayerVolumeCookieName() + "=" + volume + "; expires=" + date.toUTCString() + "; path=/";
}

function updateBackendProgress(time, vidId, lastExecutionDate) {
    dotNetHelper.invokeMethodAsync('ProgressChange', vidId, time, true);
    lastExecutionDate = new Date();
}