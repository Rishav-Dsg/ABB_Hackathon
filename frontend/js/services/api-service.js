app.service('ApiService', function($http, API_BASE_URL) {
    
    this.uploadDataset = function(file, datasetType) {
        var formData = new FormData();
        formData.append('File', file);
        formData.append('DatasetType', datasetType || 'numeric');
        
        // Use DataController route
        return $http.post(API_BASE_URL + '/api/data/upload', formData, {
            transformRequest: angular.identity,
            headers: {
                'Content-Type': undefined
            }
        });
    };

    this.getDateRange = function(request) {
        // Use DataController route
        return $http.post(API_BASE_URL + '/api/data/validate-dates', request);
    };

    this.trainModel = function(request) {
        // Use DataController route
        return $http.post(API_BASE_URL + '/api/data/train', request);
    };

    this.runSimulation = function(request) {
        // Use DataController route
        return $http.post(API_BASE_URL + '/api/data/simulate', request);
    };
});
