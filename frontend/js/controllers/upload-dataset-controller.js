app.controller('UploadDatasetController', function($scope, ApiService) {
    $scope.triggerFileSelect = function() {
        document.getElementById("fileInput").click();
    };
    
    $scope.fileUploaded = false;
    $scope.uploadedFile = null;
    $scope.busy = false;
    $scope.selectedFile = null;

    // Custom directive for file input
    $scope.$on('fileSelected', function(event, file) {
        $scope.selectedFile = file;
        $scope.handleFileUpload();
    });

    $scope.handleFileUpload = function() {
        if (!$scope.selectedFile) return;
        
        $scope.busy = true;
        
        ApiService.uploadDataset($scope.selectedFile, 'numeric')
            .then(function(response) {
                if (response.data.success) {
                    var data = response.data.data;
                    var sizeMB = ($scope.selectedFile.size / (1024 * 1024)).toFixed(2) + ' MB';
                    
                    $scope.uploadedFile = {
                        name: data.fileName,
                        size: sizeMB,
                        records: String(data.totalRecords),
                        columns: data.totalColumns,
                        passRate: data.passRate.toFixed(1) + '%',
                        dateRange: data.startDate + ' to ' + data.endDate
                    };
                    
                    $scope.fileUploaded = true;
                } else {
                    alert(response.data.message || 'Upload failed');
                }
            })
            .catch(function(error) {
                alert(error.data?.message || 'Upload failed');
            })
            .finally(function() {
                $scope.busy = false;
            });
    };

    $scope.handleReupload = function() {
        $scope.fileUploaded = false;
        $scope.uploadedFile = null;
        $scope.selectedFile = null;
    };

    $scope.nextStep = function() {
        if ($scope.fileUploaded) {
            $scope.$parent.nextStep();
        }
    };
});

// Custom directive for file input
app.directive('ngFileModel', function() {
    return {
        scope: {
            ngFileModel: '='
        },
        link: function(scope, element, attrs) {
            element.bind('change', function() {
                scope.$apply(function() {
                    scope.ngFileModel = element[0].files[0];
                    scope.$emit('fileSelected', element[0].files[0]);
                });
            });
        }
    };
});
