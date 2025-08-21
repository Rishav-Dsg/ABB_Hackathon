app.controller('MainController', function($scope) {
    $scope.currentStep = 1;
    $scope.steps = [
        'Upload Dataset',
        'Date Range',
        'Model Training',
        'Simulation'
    ];

    $scope.setCurrentStep = function(step) {
        $scope.currentStep = step;
    };

    $scope.nextStep = function() {
        if ($scope.currentStep < $scope.steps.length) {
            $scope.currentStep++;
        }
    };

    $scope.previousStep = function() {
        if ($scope.currentStep > 1) {
            $scope.currentStep--;
        }
    };
});
