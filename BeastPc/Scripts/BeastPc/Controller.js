app.controller("BeastPcController", function ($scope, BeastPCService) {

    $scope.newArray = [];
    $scope.showLogin = false;

    $scope.FName = "";
    $scope.LName = "";
    $scope.Username = "";
    $scope.Email = "";
    $scope.Password = "";

    $scope.currentUser = {};
    $scope.loginUsername = "";
    $scope.loginPassword = "";

    $scope.showClearConfirm = false;
    $scope.showRegSuccess = false;
    $scope.fieldErrors = {};

    $scope.accountAlert = { show: false, message: "" };
    $scope.dismissAccountAlert = function () {
        $scope.accountAlert.show = false;
        $scope.accountAlert.message = "";
    };
    $scope.showAccountAlert = function (msg) {
        $scope.accountAlert.message = msg || "";
        $scope.accountAlert.show = true;
    };

    $scope.checkUser = function () {
        $scope.showLogin = $scope.newArray.length >= 1;
    };

    function clearFieldErrors() {
        $scope.fieldErrors = {};
    }

    function setFieldError(key, msg) {
        if (!$scope.fieldErrors) $scope.fieldErrors = {};
        $scope.fieldErrors[key] = msg;
    }

    function validateRegistrationForm() {
        clearFieldErrors();
        var ok = true;

        var fn = ($scope.FName || "").trim();
        var ln = ($scope.LName || "").trim();
        var un = ($scope.Username || "").trim();
        var em = ($scope.Email || "").trim();
        var pw = $scope.Password || "";

        if (!fn) { setFieldError("FName", "First name is required."); ok = false; }
        else if (fn.length > 80) { setFieldError("FName", "Max 80 characters."); ok = false; }

        if (!ln) { setFieldError("LName", "Last name is required."); ok = false; }
        else if (ln.length > 80) { setFieldError("LName", "Max 80 characters."); ok = false; }

        if (!un) { setFieldError("Username", "Username is required."); ok = false; }
        else if (un.length < 3 || un.length > 32) { setFieldError("Username", "Use 3–32 characters."); ok = false; }
        else if (!/^[A-Za-z0-9_]+$/.test(un)) { setFieldError("Username", "Only letters, numbers, and underscore."); ok = false; }

        if (!em) { setFieldError("Email", "Email is required."); ok = false; }
        else if (em.length > 254) { setFieldError("Email", "Email is too long."); ok = false; }
        else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(em)) { setFieldError("Email", "Enter a valid email."); ok = false; }

        if (!pw) { setFieldError("Password", "Password is required."); ok = false; }
        else if (pw.length < 8) { setFieldError("Password", "At least 8 characters."); ok = false; }
        else if (pw.length > 128) { setFieldError("Password", "Max 128 characters."); ok = false; }

        return ok;
    };

    $scope.addFunc = function () {
        if (!validateRegistrationForm()) {
            return;
        }

        var payload = {
            FirstName: ($scope.FName || "").trim(),
            LastName: ($scope.LName || "").trim(),
            Username: ($scope.Username || "").trim(),
            Email: ($scope.Email || "").trim(),
            Password: $scope.Password
        };

        BeastPCService.register(payload).then(function (res) {
            if (res && res.data && res.data.ok) {
                $scope.FName = "";
                $scope.LName = "";
                $scope.Username = "";
                $scope.Email = "";
                $scope.Password = "";
                clearFieldErrors();
                $scope.showRegSuccess = true;
            } else {
                var msg = (res && res.data && res.data.error) ? res.data.error : "Registration failed.";
                if (res && res.data && res.data.inner) msg += "\n\nInner: " + res.data.inner;
                if (res && res.data && res.data.inner2) msg += "\n\nInner2: " + res.data.inner2;
                $scope.showAccountAlert(msg);
            }
        }, function () {
            $scope.showAccountAlert("Registration error. Try again.");
        });
    };

    $scope.closeRegSuccess = function (goLogin) {
        $scope.showRegSuccess = false;
        if (goLogin) {
            window.location.href = "/Account/LoginPage";
        }
    };

    $scope.clearFunc = function () {
        $scope.showClearConfirm = true;
    };

    $scope.confirmClear = function () {
        $scope.FName = "";
        $scope.LName = "";
        $scope.Username = "";
        $scope.Email = "";
        $scope.Password = "";
        clearFieldErrors();
        $scope.showClearConfirm = false;
    };

    $scope.cancelClear = function () {
        $scope.showClearConfirm = false;
    };

    $scope.updateFunc = function (arrayIndex) {
        if (arrayIndex == null || arrayIndex < 0 || arrayIndex >= $scope.newArray.length) return;

        var row = $scope.newArray[arrayIndex];
        row.FName = $scope.FName || "";
        row.LName = $scope.LName || "";
        row.Username = $scope.Username || "";
        if ($scope.Password) {
            row.Password = $scope.Password;
        }

        $scope.saveToSession();
    };

    $scope.loadRowIntoForm = function (user) {
        $scope.FName = user.FName || "";
        $scope.LName = user.LName || "";
        $scope.Username = user.Username || "";
        $scope.Password = user.Password || "";
    };

    $scope.deleteFunc = function (indexArray) {
        if (confirm("Delete this user?")) {
            $scope.newArray.splice(indexArray, 1);
            $scope.checkUser();
            $scope.saveToSession();
        }
    };

    $scope.saveToSession = function () {
        sessionStorage.setItem("UserArray", JSON.stringify($scope.newArray));
    };

    $scope.redirectFunc = function () {
        sessionStorage.setItem("UserArray", JSON.stringify($scope.newArray));
        window.location.href = "/Account/LoginPage";
    };

    $scope.checkArray = function () {
        var userArray = sessionStorage.getItem("UserArray");
        if (userArray !== null && userArray !== undefined && userArray !== "") {
            $scope.newArray = JSON.parse(userArray);
        } else {
            $scope.newArray = [];
        }
        $scope.checkUser();
    };

    $scope.loadCurrentUser = function () {
        var cur = sessionStorage.getItem("CurrentUser");
        if (cur !== null && cur !== undefined && cur !== "") {
            $scope.currentUser = JSON.parse(cur);
        } else {
            $scope.currentUser = {};
        }
    };

    $scope.loginFunc = function () {
        if (!$scope.loginUsername || !$scope.loginPassword) {
            $scope.showAccountAlert("Enter username and password.");
            return;
        }
        var payload = {
            UsernameOrEmail: $scope.loginUsername,
            Password: $scope.loginPassword
        };

        BeastPCService.login(payload).then(function (res) {
            if (res && res.data && res.data.ok) {
                sessionStorage.setItem("CurrentUser", JSON.stringify(res.data.user));
                var next = null;
                try {
                    var loc = new URL(window.location.href);
                    var ret = loc.searchParams.get("ReturnUrl");
                    if (ret && ret.charAt(0) === "/" && ret.indexOf("//") !== 1) {
                        next = ret;
                    }
                } catch (e) { /* ignore */ }
                if (next) {
                    window.location.href = next;
                    return;
                }
                if (res.data.user && res.data.user.Role === "admin") {
                    window.location.href = "/Admin/Dashboard";
                } else {
                    window.location.href = "/Account/Welcome";
                }
            } else {
                var err = (res && res.data && res.data.error) ? res.data.error : "Invalid username or password.";
                $scope.showAccountAlert(err);
            }
        }, function () {
            $scope.showAccountAlert("Login error. Check your connection and try again.");
        });
    };

});
