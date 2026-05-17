app.service("BeastPCService", function ($http) {
    this.register = function (payload) {
        return $http.post("/BeastPc/Register", payload);
    };

    this.login = function (payload) {
        return $http.post("/BeastPc/Login", payload);
    };
});
