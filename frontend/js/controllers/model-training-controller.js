app.controller('ModelTrainingController', function($scope, ApiService) {
    $scope.busy = false;
    $scope.trained = false;
    $scope.metrics = null;

    $scope.train = function() {
        $scope.busy = true;
        
        var body = {
            // In a fuller app, we'd read these from validated selection stored globally
            trainStart: "2021-01-01",
            trainEnd: "2021-08-31",
            testStart: "2021-09-01",
            testEnd: "2021-10-31",
            datasetType: "numeric"
        };

        ApiService.trainModel(body)
            .then(function(response) {
                if (response.data && response.data.success && response.data.data) {
                    $scope.metrics = response.data.data;
                    $scope.trained = true;
                } else {
                    alert((response.data && response.data.message) || 'Training failed');
                }
            })
            .catch(function(error) {
                alert((error.data && error.data.message) || 'Training failed');
            })
            .finally(function() {
                $scope.busy = false;
            });
    };

    $scope.nextStep = function() {
        if ($scope.trained) {
            $scope.$parent.nextStep();
        }
    };

    $scope.previousStep = function() {
        $scope.$parent.previousStep();
    };
});
