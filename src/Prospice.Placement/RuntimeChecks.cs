using UnityEngine;
using Object = UnityEngine.Object;
namespace Prospice.Placement;

internal static class RuntimeChecks
{
    static readonly List<GameObject> forceObjects = new();
    static readonly List<(Rigidbody body, float expected)> forceSamples = new();
    static float forceStart, forceWallStart;
    internal static void Advance()
    {
        if (forceSamples.Count == 0) return;
        if (Time.fixedTime <= forceStart + .02f)
        {
            if (Time.realtimeSinceStartup - forceWallStart < 30) return;
            Plugin.Instance.Log.LogError("Velocity checks could not run: no physics steps (paused game).");
            CleanupForces(); return;
        }
        try
        {
            foreach (var sample in forceSamples)
            {
                if (Vector3.Distance(sample.body.velocity, Vector3.forward * sample.expected) > .02f)
                    throw new InvalidOperationException($"Expected {sample.expected} forward velocity, got {sample.body.velocity}");
                Plugin.Instance.Log.LogInfo($"SELFTEST PASS: native {(sample.expected == 1 ? "ordinary drop" : sample.expected == 5 ? "RT throw" : "placement")} velocity after physics = {sample.body.velocity}");
            }
            Plugin.Instance.Log.LogInfo("SELFTEST COMPLETE: native release, geometry, renderer restoration and post-physics velocities passed. Physical gamepad acceptance remains manual.");
        }
        catch (Exception e) { Plugin.Enabled.Value = false; Plugin.Instance.Log.LogError("SELFTEST FAILED: " + e); }
        finally { CleanupForces(); }
    }
    internal static void Cancel() => CleanupForces();
    static void CleanupForces()
    {
        forceSamples.Clear();
        foreach (var go in forceObjects) if (go) { go.SetActive(false); Object.Destroy(go); }
        forceObjects.Clear();
    }
    internal static void Run()
    {
        var created = new List<GameObject>();
        Preview? preview = null;
        GameObject Make(string name, bool active = true) { var go = new GameObject("PlacementTest." + name); go.SetActive(active); created.Add(go); return go; }
        void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); Plugin.Instance.Log.LogInfo("SELFTEST PASS: " + message); }
        try
        {
            foreach (var real in Resources.FindObjectsOfTypeAll<PlayerInteraction>())
                Plugin.Instance.Log.LogInfo($"LIVE interaction: {real.name}; reach={real.interactionDistance}; dropMask={real.dropMask.value}; attempts={real.maxDropAttempts}");
            var helper = typeof(PlayerInteraction).Assembly.GetTypes().FirstOrDefault(t => t.GetMethod("pr") != null && t.GetMethod("pq") != null);
            if (helper != null) Plugin.Instance.Log.LogInfo("NATIVE ACTION STRINGS: pr=" + helper.GetMethod("pr")!.Invoke(null, null) + "; pq=" + helper.GetMethod("pq")!.Invoke(null, null));
            Plugin.Instance.Log.LogInfo("REWIRED: action31=" + Rewired.ReInput.mapping.GetAction(31).name + "; action30=" + Rewired.ReInput.mapping.GetAction(30).name);
            var rewiredPlayer = Rewired.ReInput.players.GetPlayer(0);
            foreach (var type in new[] { Rewired.ControllerType.Joystick, Rewired.ControllerType.Keyboard, Rewired.ControllerType.Mouse })
            {
                var last = rewiredPlayer.controllers.GetLastActiveController(type);
                Plugin.Instance.Log.LogInfo($"INPUT BINDING: {type} last controller={(last == null ? "none" : last.name)}; time={(last == null ? 0 : last.GetLastTimeActive())}");
            }
            Assert(!new GamepadInput().Read(rewiredPlayer, false).Connected || Rewired.ReInput.controllers.joystickCount > 0, "Gamepad input adapter handles controller inventory");
            var controlsHost = Make("MotionInput", false);
            var controls = controlsHost.AddComponent<FPController.FPInputManager>();
            controls.LookInput = Vector2.one; controls.MovementInput = Vector2.one;
            controls.JumpInput = controls.JumpInputHeld = controls.SprintInput = true;
            PlacementController.ClearInput(controls);
            Assert(controls.LookInput == Vector2.one && controls.MovementInput == Vector2.zero &&
                !controls.JumpInput && !controls.JumpInputHeld && !controls.SprintInput,
                "Placement preserves camera look while consuming movement, sprint and jump input");
            int jumps = 0;
            controls.OnJumpPressed = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((Action)(() => jumps++));
            var jumpHandler = InputPatch.MuteJump(controls);
            Assert(controls.OnJumpPressed == null, "Jump callback is muted only during native input polling");
            try { throw new InvalidOperationException("Simulated input polling failure"); }
            catch (InvalidOperationException) { }
            finally { InputPatch.RestoreJump(controls, jumpHandler); }
            controls.OnJumpPressed!.Invoke();
            Assert(jumps == 1, "Original jump callback restored after exceptional input exit");
            var mover = controlsHost.AddComponent<FPController.FPController>();
            mover.ocq = controls;
            mover._cameraHolderTransform = Make("LookCamera", false).transform;
            mover._mouseSensitivity = 1; mover._maxCameraY = 85;
            mover._lookDirection = Vector2.zero;
            controls.LookInput = new Vector2(3, 2);
            PlacementController.ClearInput(controls);
            mover.LateUpdate();
            Assert(mover._lookDirection == new Vector2(3, 2) && controls.MovementInput == Vector2.zero,
                "Native camera rotation consumes preserved look input while walking stays blocked");
            mover.ocp = new Vector3(2, -3, 4); mover.ocs = new Vector3(4, -2, 5); mover.oct = new Vector3(6, -1, 7);
            PlacementController.StopWalking(mover);
            Assert(mover.ocp == new Vector3(0, -3, 0) && mover.ocs == new Vector3(0, -2, 0) && mover.oct == new Vector3(0, -1, 0),
                "Horizontal inertia stops immediately while gravity state is preserved");
            var labelHost = Make("NativePromptText", false);
            labelHost.AddComponent<RectTransform>();
            var label = labelHost.AddComponent<TMPro.TextMeshProUGUI>();
            label.text = "Original X prompt";
            int componentCount = labelHost.GetComponents<Component>().Length;
            CarryHint.Write(label, "Place manually"); CarryHint.Write(label, "Release LT to place");
            Assert(label.text == "Release LT to place" && labelHost.GetComponents<Component>().Length == componentCount,
                "Native prompt updated in place without additional UI components");
            CarryHint.Dispose();
            Assert(label.text == "Original X prompt", "Prompt restores its original text after cancellation");
            CarryHint.Write(label, "Place manually"); label.text = "New vanilla prompt"; CarryHint.Dispose();
            Assert(label.text == "New vanilla prompt", "Cleanup preserves a newer vanilla prompt");
            CarryHint.Write(label, "Place manually"); label.text = "New language";
            CarryHint.Write(label, "Release LT to place"); CarryHint.Dispose();
            Assert(label.text == "New language", "Prompt restoration follows a native localization refresh");
            var origin = new Vector3(10000, 10000, 10000);
            var host = Make("Interaction", false); host.transform.position = origin;
            var camera = host.AddComponent<Camera>();
            var interaction = host.AddComponent<PlayerInteraction>();
            interaction.nlv = camera;
            interaction.nlx = rewiredPlayer;
            // The real service is created with the on-foot scene, not at the main menu.
            // Supply the same native class and player reference for this isolated formatter check.
            bool activeDevice = new[] { Rewired.ControllerType.Joystick, Rewired.ControllerType.Keyboard, Rewired.ControllerType.Mouse }
                .Any(type => rewiredPlayer.controllers.GetLastActiveController(type) != null);
            if (activeDevice)
            {
                var previousUtils = RewiredUtils.Instance;
                try
                {
                    if (!previousUtils)
                    {
                        var formatter = Make("NativeFormatter", false).AddComponent<RewiredUtils>();
                        formatter.nwe = rewiredPlayer;
                        RewiredUtils.Instance = formatter;
                    }
                    string nativeUse = interaction.bvv("Use", false);
                    string nativeLT = CarryHint.PhysicalLT(interaction, true);
                    Plugin.Instance.Log.LogInfo("NATIVE PROMPT FORMAT: Use=" + nativeUse + "; placement=" + nativeLT);
                    Assert(!string.IsNullOrWhiteSpace(nativeUse) && nativeLT.Contains("LT"), "Existing native button formatter supplies Use and physical LT prompts");
                }
                finally { RewiredUtils.Instance = previousUtils; }
            }
            else Plugin.Instance.Log.LogInfo("SELFTEST SKIP: native button formatter requires real controller activity; hidden startup has none. In-game controller acceptance remains required.");
            interaction.characterController = host.AddComponent<CharacterController>();
            interaction.maxDropAttempts = 10;
            interaction.dropCheckRadius = .2f;
            interaction.dropMask = 0;
            interaction.nlw = Vector3.one;
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); created.Add(obj);
            obj.name = "PlacementTest.Item";
            var body = obj.AddComponent<Rigidbody>();
            body.isKinematic = true; body.detectCollisions = false;
            obj.transform.SetParent(host.transform, false);
            obj.transform.localScale = Vector3.one * .3f;
            interaction.heldRigidbody = body;
            Physics.SyncTransforms();
            interaction.bvu(0);
            Assert(!interaction.heldRigidbody && !body.isKinematic && body.detectCollisions && body.transform.parent == null && body.transform.localScale == Vector3.one,
                "Native bvu(0) clears ownership, unparents, restores original scale, dynamics and collisions");
            Assert(body.interpolation == RigidbodyInterpolation.Interpolate, "Native release restores interpolation");
            body.isKinematic = true;
            var renderer = obj.GetComponent<Renderer>();
            preview = new Preview(body, Vector3.one);
            Assert(!renderer.enabled, "Preview hides carried mesh");
            Assert(preview.Root.GetComponentsInChildren<Collider>().Length == 0 && preview.Root.GetComponentsInChildren<MonoBehaviour>().Length == 0,
                "Visible silhouette contains meshes only; collision probes are separate");
            var sceneRenderers = preview.Root.GetComponentsInChildren<MeshRenderer>();
            Assert(sceneRenderers.All(r => r.enabled && r.gameObject.layer == 0), "Filled preview uses enabled world-layer scene renderers");
            foreach (bool valid in new[] { true, false })
            {
                preview.Colorize(valid);
                var expected = valid ? new Color(0, 1, 1, .32f) : new Color(1, .12f, .1f, .32f);
                Assert(sceneRenderers.All(r => r.sharedMaterial.GetColor("_Color") == expected && r.sharedMaterial.renderQueue == 3000 &&
                    r.sharedMaterial.GetInt("_ZTest") == (int)UnityEngine.Rendering.CompareFunction.LessEqual),
                    $"{(valid ? "Cyan" : "Red")} filled preview preserves transparency and scene depth before post-processing");
            }
            Assert(typeof(PlacementDriver).GetMethod("OnGUI") == null, "Placement has no post-effects GUI compositor");
            foreach (var data in Resources.FindObjectsOfTypeAll<UnityEngine.Rendering.Universal.UniversalRendererData>())
                Plugin.Instance.Log.LogInfo($"RENDER PIPELINE: {data.name}; transparentMask={data.transparentLayerMask.value}; " + UnityEngine.JsonUtility.ToJson(data));
            foreach (var feature in Resources.FindObjectsOfTypeAll<UnityEngine.Rendering.Universal.ScriptableRendererFeature>())
                Plugin.Instance.Log.LogInfo("RENDER FEATURE: " + feature.name + "; " + UnityEngine.JsonUtility.ToJson(feature));
            preview.Pose(origin + Vector3.up * .505f, Quaternion.identity);
            var shape = preview.Shapes[0];
            Assert(Mathf.Abs(PlacementSolver.Minimum(shape, Vector3.up) - (origin.y + .005f)) < .002f, "Original-size box support point");
            var floor = Make("Floor"); floor.transform.position = origin - Vector3.up * .5f;
            var floorCollider = floor.AddComponent<BoxCollider>(); floorCollider.size = new Vector3(10, 1, 10);
            Physics.SyncTransforms();
            Assert(!PlacementSolver.Penetrates(shape, floorCollider, out _), "Query probe rests above surface without penetration");
            preview.Pose(origin, Quaternion.identity);
            Physics.SyncTransforms();
            Plugin.Instance.Log.LogInfo($"COLLISION DIAG: enabled={shape.enabled}; active={shape.gameObject.activeInHierarchy}; position={shape.transform.position}; scale={shape.transform.lossyScale}; size={shape.Cast<BoxCollider>().size}; floor={floorCollider.bounds}");
            Assert(PlacementSolver.Penetrates(shape, floorCollider, out var depth) && depth > .1f,
                "Query probe rejects overlap");
            Assert(!shape.enabled, "Probe disabled before physics resumes");
            host.transform.position = origin + new Vector3(0, 2, -2);
            host.transform.rotation = Quaternion.LookRotation(new Vector3(0, -1, 1));
            PlacementTarget Solve(float reach = 5) => PlacementSolver.Solve(preview, body, host.transform, camera, reach, ~0, Quaternion.identity);
            var target = Solve();
            Assert(target.Valid && target.Support == floorCollider, "Ground raycast and support offset produce valid placement");
            PlacementTarget Snapped(Vector3 delta) => GridPlacement.Solve(preview, body, host.transform, camera, 5, ~0, Quaternion.identity, delta, Array.Empty<TruckBed>());
            var snapped = Snapped(Vector3.zero);
            Assert(snapped.Valid && Mathf.Abs(PlacementSolver.BoundsOf(preview.Shapes).min.y - floorCollider.bounds.max.y - .005f) < .003f,
                "Grid placement seats the item bottom on its physical support");
            var movedSnap = Snapped(new Vector3(.1f, .8f, 0));
            Assert(movedSnap.Valid && Mathf.Abs(movedSnap.Position.y - snapped.Position.y) < .003f &&
                Mathf.Abs((movedSnap.Position.x - snapped.Position.x) / ItemGrid.Step - Mathf.Round((movedSnap.Position.x - snapped.Position.x) / ItemGrid.Step)) < .02f,
                "Positioning snaps to the common increment and cannot leave a hovering item");
            preview.Pose(Vector3.zero, Quaternion.identity);
            var standardGrid = ItemGrid.Create(preview, floorCollider);
            preview.Pose(Vector3.zero, Quaternion.Euler(0, 90, 0));
            var turnedGrid = ItemGrid.Create(preview, floorCollider);
            Assert(standardGrid.StepX == .0625f && turnedGrid.StepX == standardGrid.StepX && turnedGrid.StepZ == standardGrid.StepZ,
                "Shared 6.25 cm lattice survives quarter-turn rotations");
            var sizedProbe = preview.Shapes[0].Cast<BoxCollider>();
            var originalProbeSize = sizedProbe.size;
            try
            {
                sizedProbe.size = new Vector3(.223f, .375f, 1.5f);
                preview.Pose(Vector3.zero, Quaternion.identity);
                var smallGrid = ItemGrid.Create(preview, floorCollider);
                preview.Pose(Vector3.zero, Quaternion.Euler(0, 90, 0));
                var wideGrid = ItemGrid.Create(preview, floorCollider);
                Assert(smallGrid.CellsX == 4 && smallGrid.CellsZ == 24 && wideGrid.CellsX == 24 && wideGrid.CellsZ == 4 && smallGrid.StepX == wideGrid.StepX,
                    "Small and long item dimensions occupy whole cells on the same grid after 90-degree rotation");
            }
            finally { sizedProbe.size = originalProbeSize; preview.Pose(Vector3.zero, Quaternion.identity); }
            var markerHost = Make("TruckStorageMarker"); markerHost.transform.position = origin + new Vector3(0, .15f, 0);
            var marker = markerHost.AddComponent<BoxCollider>(); marker.isTrigger = true; marker.size = new Vector3(2, .3f, 2);
            var floorBody = floor.AddComponent<Rigidbody>(); floorBody.isKinematic = true;
            var gridMesh = new Mesh();
            try
            {
                Physics.SyncTransforms();
                int empty = TruckStorageGrid.Build(gridMesh, marker, floor.transform, floorBody, body, host.transform);
                Assert(empty > 0 && gridMesh.vertexCount > 0, "Free truck storage generates a surface-conforming glow grid");
                var load = Make("StoredCargo"); load.transform.position = origin + new Vector3(0, .3f, 0);
                var loadCollider = load.AddComponent<BoxCollider>(); loadCollider.size = new Vector3(1, .6f, 1);
                load.AddComponent<Rigidbody>().isKinematic = true;
                Physics.SyncTransforms();
                int occupied = TruckStorageGrid.Build(gridMesh, marker, floor.transform, floorBody, body, host.transform);
                Assert(occupied < empty, "Stored cargo removes covered grid patches");
                var savedLook = host.transform.rotation;
                host.transform.rotation = Quaternion.LookRotation(loadCollider.bounds.center + Vector3.up * .3f - host.transform.position);
                var stack = GridPlacement.Solve(preview, body, host.transform, camera, 5, ~0, Quaternion.identity, Vector3.zero, Array.Empty<TruckBed>());
                Assert(stack.Valid && stack.Support == loadCollider && Mathf.Abs(PlacementSolver.BoundsOf(preview.Shapes).min.y - loadCollider.bounds.max.y - .005f) < .003f,
                    "Stacked placement seats against the actual object's top");
                host.transform.rotation = savedLook;
                loadCollider.enabled = false;
                floorCollider.enabled = false; Physics.SyncTransforms();
                Assert(TruckStorageGrid.Build(gridMesh, marker, floor.transform, floorBody, body, host.transform) == 0,
                    "Missing truck support never produces a floating storage grid");
                floorCollider.enabled = true; markerHost.SetActive(false); Physics.SyncTransforms();
            }
            finally { Object.Destroy(gridMesh); }
            preview.Pose(target.Position, target.Rotation);
            var uiHost = Make("NativeLayout", false);
            var uiRect = uiHost.AddComponent<RectTransform>(); uiRect.sizeDelta = new Vector2(1920, 1080);
            var nativeCanvas = uiHost.AddComponent<Canvas>(); nativeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var highlighter = uiHost.AddComponent<ObjectHighlighter>();
            var highlightHost = Make("NativeHighlightRect", false);
            var highlightRect = highlightHost.AddComponent<RectTransform>(); highlightRect.SetParent(uiRect, false);
            highlighter.canvas = nativeCanvas; highlighter.mainCamera = camera;
            highlighter.highlightRect = highlightRect; highlighter.objectRenderer = floorCollider;
            highlighter.Update();
            Assert(highlighter.objectRenderer == floorCollider && !float.IsNaN(highlightRect.anchoredPosition.x),
                "Native highlighter layout executes through the Harmony hook and restores its collider reference");
            Assert(!Solve(.1f).Valid, "Out-of-range target rejected");
            PlacementTarget Offset(Vector3 delta) => PlacementSolver.Solve(preview, body, host.transform, camera, 5, ~0, Quaternion.identity, delta);
            var translated = Offset(new Vector3(.4f, .3f, 0));
            Assert(translated.Valid && Vector3.Distance(translated.Position, target.Position + new Vector3(.4f, .3f, 0)) < .01f && translated.Support == floorCollider,
                "RT positioning changes X and Y while retaining valid support");
            Assert(!Offset(Vector3.down).Valid, "Manual downward penetration rejected");
            Assert(!Offset(Vector3.right * 6).Valid, "Manual position outside interaction reach rejected");
            var destinationSupport = Make("DestinationSupport"); destinationSupport.transform.position = target.Position + new Vector3(1.5f, -.4f, 0);
            var destinationCollider = destinationSupport.AddComponent<BoxCollider>(); destinationCollider.size = new Vector3(1, .2f, 1);
            Physics.SyncTransforms();
            var differentSupport = Offset(new Vector3(1.5f, .5f, 0));
            Assert(differentSupport.Valid && differentSupport.Support == destinationCollider, "Translated placement reacquires its actual destination support");
            destinationCollider.enabled = false;
            var wall = Make("SightBlocker"); wall.transform.position = (camera.transform.position + translated.Position) * .5f;
            var wallCollider = wall.AddComponent<BoxCollider>(); wallCollider.size = Vector3.one * .15f;
            Physics.SyncTransforms();
            Assert(!Offset(new Vector3(.4f, .3f, 0)).Valid, "Manual position cannot bypass intervening geometry");
            wallCollider.enabled = false;

            var blocker = Make("Blocker"); blocker.transform.position = target.Position + Vector3.right * .8f;
            var blockerCollider = blocker.AddComponent<BoxCollider>(); blockerCollider.size = Vector3.one * .8f;
            Physics.SyncTransforms();
            Assert(!Solve().Valid, "Blocking adjacent geometry rejected without moving the held item");
            blockerCollider.enabled = false;
            var trigger = Make("Trigger"); trigger.transform.position = target.Position;
            trigger.AddComponent<BoxCollider>().isTrigger = true;
            Physics.SyncTransforms();
            Assert(Solve().Valid, "Trigger-only overlap ignored");
            floor.transform.position += Vector3.up * .5f;
            Physics.SyncTransforms();
            var moved = Solve();
            Assert(moved.Valid && Mathf.Abs(moved.Position.y - target.Position.y - .5f) < .01f, "Moving support re-solved at current position");
            foreach (float angle in new[] { 59f, 61f })
            {
                floor.transform.rotation = Quaternion.Euler(angle, 0, 0);
                var normal = floor.transform.up;
                var point = floor.transform.TransformPoint(Vector3.up * .5f);
                host.transform.position = point + normal * 2;
                host.transform.rotation = Quaternion.LookRotation(-normal, Vector3.right);
                Physics.SyncTransforms();
                Assert(Solve().Valid == (angle < 60), $"Slope {angle} degrees classified correctly");
            }
            floor.transform.rotation = Quaternion.identity;
            host.transform.position = origin + new Vector3(0, 3, -2);
            host.transform.rotation = Quaternion.LookRotation(new Vector3(0, -1, 1));
            Physics.SyncTransforms();
            target = Solve();
            Assert(target.Valid, "Shelf-height support remains valid");
            interaction.heldRigidbody = body; body.isKinematic = true; body.detectCollisions = false;
            body.transform.SetParent(host.transform, true); body.transform.localScale = Vector3.one * .3f;
            interaction.maxDropAttempts = 0;
            Assert(!GameIntegration.Commit(interaction, body, target) && interaction.heldRigidbody == body && body.isKinematic && !body.detectCollisions,
                "Native declined release preserves held ownership and collisions");
            interaction.maxDropAttempts = 10;
            var controllerBefore = interaction.characterController;
            Assert(GameIntegration.Commit(interaction, body, target), "Placement commit uses verified native release successfully");
            Assert(!interaction.heldRigidbody && body.transform.parent == null && body.useGravity && !body.isKinematic && body.detectCollisions &&
                Vector3.Distance(body.position, target.Position) < .01f && body.velocity.sqrMagnitude < .001f && interaction.characterController == controllerBefore,
                "Commit applies target pose, restores real controller, gravity and collisions without launch velocity");
            Assert(!GameIntegration.Commit(interaction, body, target), "Second commit refused after ownership cleared");
            preview.Dispose(); preview = null;
            Assert(renderer.enabled, "Cancellation restores renderer state");
            renderer.enabled = false;
            var visibleChild = GameObject.CreatePrimitive(PrimitiveType.Cube); created.Add(visibleChild);
            visibleChild.transform.SetParent(body.transform, false);
            visibleChild.GetComponent<Collider>().enabled = false;
            preview = new Preview(body, Vector3.one);
            preview.Dispose(); preview = null;
            Assert(!renderer.enabled && visibleChild.GetComponent<Renderer>().enabled, "Mixed original renderer states restored exactly");
            GameObject ForceMake(string name, bool active = true) { var go = new GameObject("PlacementTest." + name); go.SetActive(active); forceObjects.Add(go); return go; }
            forceStart = Time.fixedTime; forceWallStart = Time.realtimeSinceStartup;
            foreach (var force in new[] { 1f, 5f, 0f })
            {
                var sampleHost = ForceMake("ForceHost", false); sampleHost.transform.position = origin + Vector3.up * 30 + Vector3.right * (force * 3);
                var sampleCamera = sampleHost.AddComponent<Camera>();
                var sampleInteraction = sampleHost.AddComponent<PlayerInteraction>();
                sampleInteraction.nlv = sampleCamera;
                sampleInteraction.characterController = sampleHost.AddComponent<CharacterController>();
                sampleInteraction.nlw = Vector3.one;
                sampleInteraction.maxDropAttempts = 10; sampleInteraction.dropMask = 0;
                var sampleObject = ForceMake("ForceSample");
                sampleObject.AddComponent<BoxCollider>();
                var sampleBody = sampleObject.AddComponent<Rigidbody>(); sampleBody.useGravity = false;
                sampleBody.isKinematic = true; sampleBody.detectCollisions = false;
                sampleObject.transform.SetParent(sampleHost.transform, false);
                sampleInteraction.heldRigidbody = sampleBody;
                if (force == 0) Assert(GameIntegration.Commit(sampleInteraction, sampleBody,
                    new PlacementTarget(true, sampleHost.transform.position + Vector3.forward, Quaternion.identity, floorCollider, "test")), "Zero-force placement sample committed");
                else sampleInteraction.bvu(force);
                forceSamples.Add((sampleBody, force));
            }
            Plugin.Instance.Log.LogInfo("Controller inventory: " + Rewired.ReInput.controllers.joystickCount + " connected joystick(s). Physical input/feel acceptance is separate.");
            Plugin.Instance.Log.LogInfo("SELFTEST geometry/release checks passed; waiting for post-physics velocity checks.");
        }
        catch (Exception e) { CleanupForces(); Plugin.Enabled.Value = false; Plugin.Instance.Log.LogError("SELFTEST FAILED; placement disabled: " + e); }
        finally { preview?.Dispose(); foreach (var go in created) if (go) { go.SetActive(false); Object.Destroy(go); } }
    }
}
