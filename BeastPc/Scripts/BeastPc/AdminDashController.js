angular.module("BeastPcAdmin").controller("AdminDashController", function ($scope, AdminService) {

    // ===== page state =====
    $scope.activeTab = "overview";
    $scope.searchBuilds = "";
    $scope.searchOrders = "";
    $scope.searchUsers = "";

    // ===== stats =====
    $scope.statBuilds = 0;
    $scope.statOrders = 0;
    $scope.statUsers = 0;
    $scope.statRevenue = 0;

    // ===== cards + charts =====
    $scope.cardData = [];
    $scope.orderStatusLabels = [];
    $scope.orderStatusData = [];
    $scope.orderStatusColors = ['#ffd24d', '#7dd3fc', '#60a5fa', '#86efac', '#fca5a5'];
    $scope.chartPlaceholder = false;
    $scope.orderStatusOptions = {
        responsive: true,
        maintainAspectRatio: false,
        animation: { duration: 550, easing: "easeOutQuart" },
        legend: { display: false },
        scales: {
            xAxes: [{
                ticks: {
                    fontColor: "rgba(255,255,255,0.6)",
                    beginAtZero: true,
                    precision: 0
                },
                gridLines: { color: "rgba(255,255,255,0.07)", zeroLineColor: "rgba(255,255,255,0.12)" }
            }],
            yAxes: [{
                ticks: {
                    fontColor: "rgba(255,255,255,0.78)",
                    fontSize: 11
                },
                gridLines: { display: false, zeroLineDisplay: false }
            }]
        },
        tooltips: {
            backgroundColor: "rgba(14,14,14,0.94)",
            titleFontFamily: "'Bebas Neue', system-ui, sans-serif",
            bodyFontFamily: "system-ui, sans-serif",
            xPadding: 12,
            yPadding: 10,
            cornerRadius: 8,
            callbacks: {
                label: function (tooltipItem, data) {
                    var dataset = data.datasets[tooltipItem.datasetIndex];
                    var value = Number(dataset.data[tooltipItem.index]) || 0;
                    var total = dataset.data.reduce(function (sum, v) {
                        return sum + (Number(v) || 0);
                    }, 0);
                    var pct = total > 0 ? Math.round((value / total) * 100) : 0;
                    return "Orders: " + value + " (" + pct + "% of total)";
                }
            }
        }
    };

    // ===== scoped confirm overlay (replaces native confirm) =====
    $scope.adminConfirm = {
        open: false,
        title: "",
        message: "",
        confirmLabel: "OK",
        cancelLabel: "Cancel",
        destructive: false,
        _onOk: null
    };

    function humanStatus(s) {
        if (!s) return "";
        return s.charAt(0).toUpperCase() + s.slice(1);
    }

    function rawOrderStatus(o) {
        if (!o) return "";
        var v = o.Status !== undefined && o.Status !== null ? o.Status : o.status;
        if (v !== undefined && v !== null && String(v).trim() !== "") return v;
        var k;
        for (k in o) {
            if (!Object.prototype.hasOwnProperty.call(o, k)) continue;
            if (String(k).toLowerCase() === "status") {
                var x = o[k];
                if (x !== undefined && x !== null && String(x).trim() !== "") return x;
            }
        }
        return "";
    }

    function normalizeOrderStatus(s) {
        if (s == null || s === "") return "";
        return String(s).replace(/\u200b/g, "").trim().toLowerCase();
    }

    $scope.orderStatusNorm = function (o) {
        return normalizeOrderStatus(rawOrderStatus(o));
    };

    var ORDER_STATUS_KEYS = ["pending", "processing", "shipped", "delivered", "cancelled"];
    $scope.orderStatusSelectOptions = [
        { value: "pending", label: "Pending" },
        { value: "processing", label: "Processing" },
        { value: "shipped", label: "Shipped" },
        { value: "delivered", label: "Delivered" },
        { value: "cancelled", label: "Cancelled" }
    ];
    $scope.orderStatusDraft = {};

    $scope.syncOrderStatusDrafts = function () {
        var map = {};
        ($scope.orders || []).forEach(function (o) {
            var k = normalizeOrderStatus(rawOrderStatus(o));
            if (ORDER_STATUS_KEYS.indexOf(k) < 0) k = "pending";
            map[o.Id] = k;
        });
        $scope.orderStatusDraft = map;
    };

    $scope.orderStatusDirty = function (o) {
        var sel = $scope.orderStatusDraft[o.Id];
        return (sel || "") !== ($scope.orderStatusNorm(o) || "");
    };

    $scope.openAdminConfirm = function (opts, onOk) {
        opts = opts || {};
        $scope.adminConfirm.title = opts.title || "Confirm";
        $scope.adminConfirm.message = opts.message || "";
        $scope.adminConfirm.confirmLabel = opts.confirmLabel || "OK";
        $scope.adminConfirm.cancelLabel = opts.cancelLabel || "Cancel";
        $scope.adminConfirm.destructive = !!opts.destructive;
        $scope.adminConfirm._onOk = onOk;
        $scope.adminConfirm.open = true;
    };

    $scope.dismissAdminConfirm = function () {
        $scope.adminConfirm.open = false;
        $scope.adminConfirm._onOk = null;
    };

    $scope.acceptAdminConfirm = function () {
        var fn = $scope.adminConfirm._onOk;
        $scope.adminConfirm.open = false;
        $scope.adminConfirm._onOk = null;
        if (typeof fn === "function") fn();
    };

    $scope.onAdminConfirmBackdrop = function (e) {
        if (e.target === e.currentTarget) {
            $scope.dismissAdminConfirm();
        }
    };

    $scope.cardDefs = [];
    $scope.cardModalOpen = false;
    $scope.cardEditor = {
        Id: null,
        SortOrder: 0,
        Title: "",
        Subtitle: "",
        Accent: "rgba(255,255,255,0.12)",
        MetricKey: "builds",
        LiteralValue: "",
        IsActive: true
    };

    // ===== data =====
    $scope.builds = [];
    $scope.orders = [];
    $scope.users = [];

    // ===== form (build modal) =====
    $scope.buildModalOpen = false;
    $scope.buildForm = {
        Id: null,
        Name: "",
        Description: "",
        Price: 0,
        Stock: 0,
        Cpu: "",
        Gpu: "",
        Ram: "",
        Storage: "",
        Cooling: "",
        Psu: "",
        CaseName: "",
        ImageUrl: "",
        Active: true
    };

    // ===== helpers =====
    $scope.peso = function (v) {
        return "\u20B1" + Number(v || 0).toLocaleString();
    };
    function parseMvcDate(d) {
        // "/Date(1713830400000)/"
        if (!d) return null;
        var m = /Date\((\d+)\)/.exec(d);
        return m ? new Date(parseInt(m[1], 10)) : null;
    }
    $scope.toDate = function (d) {
        var dt = parseMvcDate(d);
        return dt ? dt.toLocaleDateString() : "—";
    };

    // ===== tab =====
    $scope.switchTab = function (name) {
        $scope.activeTab = name;
    };

    // ===== cards =====
    $scope.getCardDataFunc = function () {
        AdminService.getDashboardCards().then(function (res) {
            if (res && res.data && res.data.ok) {
                $scope.cardData = res.data.cards || [];
            }
        }, function () { });
    };

    // ===== chart =====
    $scope.getChartDataFunc = function () {
        AdminService.getOrdersStatusChart().then(function (res) {
            if (res && res.data && res.data.ok) {
                var labels = res.data.labels || [];
                var data = res.data.data || [];
                if (!labels.length) {
                    $scope.chartPlaceholder = true;
                    $scope.orderStatusLabels = ["No orders yet"];
                    $scope.orderStatusData = [1];
                    $scope.orderStatusColors = ["rgba(255,255,255,0.12)"];
                } else {
                    $scope.chartPlaceholder = false;
                    $scope.orderStatusLabels = labels.map(function (l) {
                        if (!l) return "";
                        return l.charAt(0).toUpperCase() + l.slice(1);
                    });
                    $scope.orderStatusData = angular.copy(data);
                    $scope.orderStatusColors = ['#ffd24d', '#7dd3fc', '#60a5fa', '#86efac', '#fca5a5'];
                }
            }
        }, function () { });
    };

    // ===== builds CRUD (prof style: add/update/delete/clear) =====
    $scope.clearFunc = function () {
        $scope.buildForm = {
            Id: null, Name: "", Description: "", Price: 0, Stock: 0,
            Cpu: "", Gpu: "", Ram: "", Storage: "", Cooling: "", Psu: "", CaseName: "", ImageUrl: "", Active: true
        };
        resetPreview();
    };

    function resetPreview() {
        var img = document.getElementById("imgPreviewEl");
        var box = document.getElementById("imgPreviewBox");
        if (img) { img.src = ""; img.classList.remove("loaded"); }
        if (box) { box.classList.remove("has-img"); }
        try {
            if (_localPreviewUrl) { URL.revokeObjectURL(_localPreviewUrl); _localPreviewUrl = ""; }
        } catch (e) { }
    }

    $scope.previewImage = function () {
        var url = ($scope.buildForm.ImageUrl || "").trim();
        var img = document.getElementById("imgPreviewEl");
        var box = document.getElementById("imgPreviewBox");
        if (!img || !box) return;
        if (!url) { resetPreview(); return; }
        img.onload = function () {
            img.classList.add("loaded");
            box.classList.add("has-img");
        };
        img.onerror = function () {
            img.classList.remove("loaded");
            box.classList.remove("has-img");
        };
        img.src = url;
    };

    $scope.closeModalOnBg = function (e) {
        if (e.target === e.currentTarget) $scope.closeModal();
    };

    $scope.isUploadingImage = false;
    $scope.uploadImageErr = "";
    var _localPreviewUrl = "";

    $scope.onImageFileChange = function (el) {
        $scope.uploadImageErr = "";
        var file = el && el.files && el.files[0];
        if (!file) return;

        // Quick local preview while uploading
        try {
            if (_localPreviewUrl) URL.revokeObjectURL(_localPreviewUrl);
            _localPreviewUrl = URL.createObjectURL(file);
            // Preview blob WITHOUT saving it into ImageUrl
            $scope.previewImageUrl(_localPreviewUrl);
        } catch (e) { }

        $scope.isUploadingImage = true;
        $scope.$applyAsync();

        AdminService.uploadBuildImage(file).then(function (res) {
            $scope.isUploadingImage = false;
            if (res && res.data && res.data.ok) {
                $scope.buildForm.ImageUrl = res.data.url;
                $scope.previewImage();
            } else {
                $scope.uploadImageErr = (res && res.data && res.data.error) ? res.data.error : "Upload failed.";
            }
        }, function () {
            $scope.isUploadingImage = false;
            $scope.uploadImageErr = "Upload error.";
        });
    };

    $scope.openAddModal = function () {
        $scope.clearFunc();
        $scope.buildModalOpen = true;
    };

    $scope.openEditModal = function (b) {
        $scope.buildForm = angular.copy(b);
        $scope.buildModalOpen = true;
        // slight delay to let DOM render before setting preview
        setTimeout(function () { $scope.previewImage(); }, 50);
    };

    $scope.closeModal = function () {
        $scope.buildModalOpen = false;
    };

    // Using upsert endpoint but still exposing addFunc/updateFunc like the lecture pattern
    $scope.addFunc = function () {
        $scope.buildForm.Id = null;
        return $scope.saveBuild();
    };

    $scope.updateFunc = function () {
        if (!$scope.buildForm.Id) {
            alert("Select a build to update (Edit).");
            return;
        }
        return $scope.saveBuild();
    };

    $scope.saveBuild = function () {
        if (!$scope.buildForm.Name) {
            alert("Build name is required.");
            return;
        }
        if (($scope.buildForm.ImageUrl || "").indexOf("blob:") === 0) {
            alert("Image is still uploading (blob URL). Please wait until upload finishes.");
            return;
        }
        AdminService.upsertBuild($scope.buildForm).then(function (res) {
            if (res && res.data && res.data.ok) {
                $scope.buildModalOpen = false;
                $scope.getBuilds();
            } else {
                alert((res && res.data && res.data.error) ? res.data.error : "Save failed.");
            }
        }, function (err) {
            var msg = "Save error.";
            try {
                if (err && err.data) {
                    if (typeof err.data === "string") msg = err.data;
                    else if (err.data.error) msg = err.data.error;
                    else msg = JSON.stringify(err.data);
                }
            } catch (e) { }
            alert(msg);
        });
    };

    // Preview helper for local blob URL
    $scope.previewImageUrl = function (url) {
        var img = document.getElementById("imgPreviewEl");
        var box = document.getElementById("imgPreviewBox");
        if (!img || !box) return;
        if (!url) { return; }
        img.onload = function () {
            img.classList.add("loaded");
            box.classList.add("has-img");
        };
        img.onerror = function () {
            img.classList.remove("loaded");
            box.classList.remove("has-img");
        };
        img.src = url;
    };

    $scope.deleteFunc = function (id) {
        $scope.openAdminConfirm({
            title: "Delete build",
            message: "This build will be removed from the storefront. Continue?",
            confirmLabel: "Delete",
            destructive: true
        }, function () {
            AdminService.deleteBuild(id).then(function (res) {
                if (res && res.data && res.data.ok) $scope.getBuilds();
                else alert((res && res.data && res.data.error) ? res.data.error : "Delete failed.");
            }, function () { alert("Delete error."); });
        });
    };

    $scope.getBuilds = function () {
        AdminService.getBuilds().then(function (res) {
            if (res && res.data && res.data.ok) {
                $scope.builds = res.data.builds || [];
                $scope.statBuilds = $scope.builds.length;
            }
        }, function () {});
    };

    // ===== users =====
    $scope.getUsers = function () {
        AdminService.searchUsers("").then(function (res) {
            if (res && res.data && res.data.ok) {
                $scope.users = res.data.users || [];
                $scope.statUsers = $scope.users.length;
            }
        }, function () {});
    };

    $scope.toggleRole = function (u) {
        var newRole = (u.Role === "admin") ? "user" : "admin";
        $scope.openAdminConfirm({
            title: "Change user role",
            message: 'Set this account to "' + newRole + '"?',
            confirmLabel: "Change role"
        }, function () {
            AdminService.changeUserRole(u.Id, newRole).then(function (res) {
                if (res && res.data && res.data.ok) $scope.getUsers();
                else alert((res && res.data && res.data.error) ? res.data.error : "Role update failed.");
            }, function () { alert("Role update error."); });
        });
    };

    // ===== orders =====
    $scope.getOrders = function () {
        AdminService.getOrders().then(function (res) {
            if (res && res.data && res.data.ok) {
                $scope.orders = res.data.orders || [];
                $scope.statOrders = $scope.orders.length;
                $scope.statRevenue = Math.round($scope.orders
                    .filter(function (o) { return normalizeOrderStatus(rawOrderStatus(o)) === "delivered"; })
                    .reduce(function (s, o) { return s + parseFloat(o.TotalAmount || 0); }, 0));
                $scope.syncOrderStatusDrafts();
            }
        }, function () {});
    };

    $scope.applyOrderStatus = function (o) {
        var sel = $scope.orderStatusDraft[o.Id];
        var cur = $scope.orderStatusNorm(o);
        if (!sel || sel === cur) return;
        $scope.openAdminConfirm({
            title: "Update order",
            message: "Set status to " + humanStatus(sel) + "?",
            confirmLabel: "Save status"
        }, function () {
            AdminService.updateOrderStatus(o.Id, sel).then(function (res) {
                if (res && res.data && res.data.ok) {
                    $scope.getOrders();
                    $scope.getChartDataFunc();
                } else alert((res && res.data && res.data.error) ? res.data.error : "Status update failed.");
            }, function () { alert("Status update error."); });
        });
    };

    // ===== dashboard card definitions (admin-managed) =====
    $scope.loadCardDefs = function () {
        AdminService.getDashboardCardDefinitions().then(function (res) {
            if (res && res.data && res.data.ok) {
                $scope.cardDefs = res.data.cards || [];
            }
        }, function () { });
    };

    $scope.openNewDashboardCard = function () {
        $scope.cardEditor = {
            Id: null,
            SortOrder: ($scope.cardDefs && $scope.cardDefs.length) ? $scope.cardDefs.length : 0,
            Title: "",
            Subtitle: "",
            Accent: "rgba(255,255,255,0.12)",
            MetricKey: "builds",
            LiteralValue: "",
            IsActive: true
        };
        $scope.cardModalOpen = true;
    };

    $scope.editDashboardCard = function (c) {
        $scope.cardEditor = angular.copy(c);
        $scope.cardModalOpen = true;
    };

    $scope.closeDashboardCardModal = function () {
        $scope.cardModalOpen = false;
    };

    $scope.saveDashboardCardDef = function () {
        AdminService.saveDashboardCard($scope.cardEditor).then(function (res) {
            if (res && res.data && res.data.ok) {
                $scope.cardModalOpen = false;
                $scope.loadCardDefs();
                $scope.getCardDataFunc();
            } else {
                alert((res && res.data && res.data.error) ? res.data.error : "Save failed.");
            }
        }, function () { alert("Save error."); });
    };

    $scope.deleteDashboardCardDef = function (c) {
        if (!c || !c.Id) return;
        $scope.openAdminConfirm({
            title: "Delete dashboard card",
            message: "\"" + (c.Title || "Card") + "\" will be removed from the KPI row. Continue?",
            confirmLabel: "Delete",
            destructive: true
        }, function () {
            AdminService.deleteDashboardCard(c.Id).then(function (res) {
                if (res && res.data && res.data.ok) {
                    $scope.loadCardDefs();
                    $scope.getCardDataFunc();
                } else {
                    alert((res && res.data && res.data.error) ? res.data.error : "Delete failed.");
                }
            }, function () { alert("Delete error."); });
        });
    };

    // ===== init =====
    $scope.getCardDataFunc();
    $scope.getChartDataFunc();
    $scope.loadCardDefs();
    $scope.getBuilds();
    $scope.getOrders();
    $scope.getUsers();
});

