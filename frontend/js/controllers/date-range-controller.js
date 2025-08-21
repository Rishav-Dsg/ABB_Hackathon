app.controller('DateRangeController', function($scope, ApiService) {
    $scope.training = { startDate: '', endDate: '' };
    $scope.testing = { startDate: '', endDate: '' };
    $scope.simulation = { startDate: '', endDate: '' };
    $scope.busy = false;
    $scope.ok = false;

    $scope.validate = function() {
        $scope.busy = true;
        function toYmd(value) {
            if (!value) return '';
            if (typeof value === 'string') {
                if (/^\d{4}-\d{2}-\d{2}$/.test(value)) return value; // already yyyy-mm-dd
                var m = value.match(/^(\d{2})-(\d{2})-(\d{4})$/); // dd-mm-yyyy
                if (m) return m[3] + '-' + m[2] + '-' + m[1];
            }
            var d = new Date(value);
            var y = d.getFullYear();
            var m2 = ('0' + (d.getMonth() + 1)).slice(-2);
            var day = ('0' + d.getDate()).slice(-2);
            return y + '-' + m2 + '-' + day;
        }
        
        var body = {
            trainingStart: toYmd($scope.training.startDate),
            trainingEnd: toYmd($scope.training.endDate),
            testingStart: toYmd($scope.testing.startDate),
            testingEnd: toYmd($scope.testing.endDate),
            simulationStart: toYmd($scope.simulation.startDate),
            simulationEnd: toYmd($scope.simulation.endDate),
            datasetType: 'numeric'
        };

        ApiService.getDateRange(body)
            .then(function(response) {
                if (response.data && response.data.success && response.data.data && response.data.data.isValid) {
                    $scope.ok = true;
                } else {
                    alert((response.data && response.data.message) || 'Validation failed');
                    $scope.ok = false;
                }
            })
            .catch(function(error) {
                alert((error.data && error.data.message) || 'Validation failed');
                $scope.ok = false;
            })
            .finally(function() {
                $scope.busy = false;
            });
    };

    $scope.nextStep = function() {
        if ($scope.ok) {
            $scope.$parent.nextStep();
        }
    };

    $scope.previousStep = function() {
        $scope.$parent.previousStep();
    };
});
