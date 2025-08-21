var app = angular.module('abbApp', ['ngResource']);

// Global configuration
app.constant('API_BASE_URL', 'http://localhost:5000');

// Global filters
app.filter('filesize', function() {
    return function(bytes) {
        if (bytes === 0) return '0 Bytes';
        var k = 1024;
        var sizes = ['Bytes', 'KB', 'MB', 'GB'];
        var i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
    };
});
