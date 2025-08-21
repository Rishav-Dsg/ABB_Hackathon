app.controller('RealTimeSimulationController', function($scope, ApiService) {
    $scope.simulationStarted = false;
    $scope.isSimulating = false;
    $scope.predictionData = [];
    $scope.predictionStream = [];
    $scope.liveStats = { total: 0, passed: 0, failed: 0, accuracy: 0 };

    $scope.startSimulation = function() {
        $scope.isSimulating = true;
        $scope.simulationStarted = true;
        
        var body = { 
            simulationStart: "2021-11-01", 
            simulationEnd: "2021-12-31", 
            datasetType: "numeric" 
        };

        ApiService.runSimulation(body)
            .then(function(response) {
                if (response.data && response.data.success && response.data.data) {
                    var rows = response.data.data.records || [];
                    $scope.predictionStream = rows;
                    $scope.predictionData = rows.slice(0, 10).map(function(r) {
                        return { time: r.timestamp, quality: Math.round(r.confidence) };
                    });
                    var s = response.data.data.summary || {};
                    var acc = s.totalPredictions ? Math.round((s.passCount / s.totalPredictions) * 100) : 0;
                    $scope.liveStats = { 
                        total: s.totalPredictions || 0, 
                        passed: s.passCount || 0, 
                        failed: s.failCount || 0, 
                        accuracy: acc 
                    };
                } else {
                    alert((response.data && response.data.message) || 'Simulation failed');
                }
            })
            .catch(function(error) {
                alert((error.data && error.data.message) || 'Simulation failed');
            })
            .finally(function() {
                $scope.isSimulating = false;
            });
    };

    $scope.previousStep = function() {
        $scope.$parent.previousStep();
    };
});
