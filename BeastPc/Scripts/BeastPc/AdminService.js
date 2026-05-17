angular.module("BeastPcAdmin").service("AdminService", function ($http) {
    // Builds
    this.getBuilds = function () {
        return $http.get("/BeastPc/GetBuilds");
    };
    this.uploadBuildImage = function (file) {
        var fd = new FormData();
        fd.append("file", file);
        return $http.post("/BeastPc/UploadBuildImage", fd, {
            transformRequest: angular.identity,
            headers: { "Content-Type": undefined }
        });
    };
    this.upsertBuild = function (payload) {
        return $http.post("/BeastPc/UpsertBuild", payload);
    };
    this.deleteBuild = function (id) {
        return $http.post("/BeastPc/DeleteBuild", { Id: id });
    };

    // Orders
    this.getOrders = function () {
        return $http.get("/BeastPc/GetOrders");
    };
    this.updateOrderStatus = function (id, status) {
        return $http.post("/BeastPc/UpdateOrderStatus", {
            Id: id != null ? Number(id) : 0,
            Status: (status == null ? "" : String(status))
        });
    };

    // Users
    this.searchUsers = function (q) {
        return $http.get("/BeastPc/SearchUsers", { params: { q: q || "" } });
    };
    this.changeUserRole = function (id, role) {
        return $http.post("/BeastPc/ChangeUserRole", { Id: id, Role: role });
    };

    // Dashboard
    this.getDashboardCards = function () {
        return $http.get("/BeastPc/GetDashboardCards");
    };
    this.getDashboardCardDefinitions = function () {
        return $http.get("/BeastPc/GetDashboardCardDefinitions");
    };
    this.saveDashboardCard = function (payload) {
        return $http.post("/BeastPc/SaveDashboardCard", payload);
    };
    this.deleteDashboardCard = function (id) {
        return $http.post("/BeastPc/DeleteDashboardCard", { Id: id });
    };
    this.getOrdersStatusChart = function () {
        return $http.get("/BeastPc/GetOrdersStatusChart");
    };
});

