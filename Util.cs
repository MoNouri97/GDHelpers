using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace GDHelpers
{
    public static class Util
    {
        private static readonly Dictionary<Node, Tween> tweens = [];

        public static Node3D InstantiatePackedInRoot(
            this Node instance,
            PackedScene scene,
            Vector3 pos,
            Vector3 rot
        )
        {
            Window root = instance.GetTree().Root;
            return root.InstantiatePacked(scene, pos, rot);
        }

        public static Node InstantiatePackedInRoot(this Node instance, PackedScene scene)
        {
            Window root = instance.GetTree().Root;
            return root.InstantiatePacked(scene);
        }

        public static Node3D InstantiatePacked(
            this Node instance,
            PackedScene scene,
            Vector3 pos,
            Vector3 rot
        )
        {
            if (scene == null)
            {
                return null;
            }
            var node = scene.Instantiate() as Node3D;
            instance.AddChildNode(node, pos, rot);
            return node;
        }

        public static Node InstantiatePacked(this Node instance, PackedScene scene)
        {
            if (scene == null)
            {
                return null;
            }
            var node = scene.Instantiate();
            instance.AddChild(node);
            node.Owner = instance;
            return node;
        }

        /// <summary>
        /// Add Child + sets owner
        /// </summary>
        public static void AddChildNode<T>(
            this Node instance,
            T node,
            Vector3? pos = null,
            Vector3? rot = null
        )
            where T : Node
        {
            instance.AddChild(node);
            node.Owner = instance;
            if (node is Node3D node3D && pos != null && rot != null)
            {
                node3D.GlobalPosition = (Vector3)pos;
                node3D.GlobalRotation = (Vector3)rot;
            }
        }

        public static void SetYRotation(this Node3D instance, float rotation)
        {
            instance.Rotation = new(instance.Rotation.X, rotation, instance.Rotation.Z);
        }

        public static void LookAtOnYAxis(this Node3D instance, Vector3 targetPosition, float weight)
        {
            // Look at the target
            instance.LookAt(targetPosition, Vector3.Up);

            // Get current rotation and modify
            Vector3 currentRotation = instance.RotationDegrees;

            // Keep only Y rotation, reset X and Z
            currentRotation.X = 0;
            currentRotation.Z = 0;

            // Apply the modified rotation
            instance.RotationDegrees = currentRotation;
        }

        public static void LerpToLookAt(this Node3D instance, Vector3 targetPos, float weight)
        {
            if (
                targetPos.IsZeroApprox()
                || instance.GlobalBasis.Y.DirectionTo(targetPos).IsZeroApprox()
                || instance.GlobalPosition.DirectionTo(targetPos).IsZeroApprox()
            )
            {
                return;
            }
            try
            {
                instance.GlobalTransform = instance.GlobalTransform.InterpolateWith(
                    instance.GlobalTransform.LookingAt(targetPos, instance.GlobalBasis.Y),
                    weight
                );
            }
            catch (Exception)
            {
                return;
            }
        }

        public static Basis AlignUp(this Node3D instance, Vector3 normal)
        {
            var nodeBasis = instance.GlobalBasis;
            nodeBasis.Y = normal;
            Vector3 potentialZ = -nodeBasis.X.Cross(normal);
            Vector3 potentialX = -nodeBasis.Z.Cross(normal);

            if (potentialZ.Length() > potentialX.Length())
            {
                nodeBasis.X = potentialZ;
            }
            else
            {
                nodeBasis.X = potentialX;
            }

            nodeBasis.Z = nodeBasis.X.Cross(nodeBasis.Y);
            nodeBasis = nodeBasis.Orthonormalized();

            return nodeBasis;
        }

        // public static float GetAngleOnAxis(Vector3 vectorA, Vector3 vectorB, string axis)
        // {
        //   float dotProduct = vectorA.Normalized().Dot(vectorB.Normalized());
        //   float angle = (float)Math.Acos(dotProduct);
        //
        //   if (axis == "x")
        //   {
        //     angle = (float)Math.Atan2(vectorA.y, vectorA.z) - (float)Math.Atan2(vectorB.y, vectorB.z);
        //   }
        //   else if (axis == "y")
        //   {
        //     angle = (float)Math.Atan2(vectorA.z, vectorA.x) - (float)Math.Atan2(vectorB.z, vectorB.x);
        //   }
        //   else if (axis == "z")
        //   {
        //     angle = (float)Math.Atan2(vectorA.x, vectorA.y) - (float)Math.Atan2(vectorB.x, vectorB.y);
        //   }
        //
        //   return angle;
        // }

        public static string FormatTimeString(double time, string delimiter = "'")
        {
            // Calculate minutes and remaining seconds
            int minutes = (int)(time / 60);
            float seconds = (float)(time % 60);
            // Format as "minutes:seconds" string
            var formatted = $"{minutes:D2}{delimiter}{seconds:F2}";
            return formatted;
        }

        public static Tween AnimateTo(
            this Control node,
            StringName property,
            Variant value,
            double duration = .2,
            double Delay = 0,
            Variant? from = null,
            Action callback = null,
            Tween.EaseType EaseType = Tween.EaseType.Out,
            Tween.TransitionType TransType = Tween.TransitionType.Spring,
            bool center = true,
            bool isIndependent = false,
            float speedScale = 1,
            bool overrideSpeed = false,
            Tween tween = null,
            GodotObject obj = null
        )
        {
            Tween t;
            if (tween != null)
            {
                t = tween;
            }
            else if (isIndependent)
            {
                t = node.CreateTween();
            }
            else
            {
                t = GetTween(node);
            }

            if (center)
            {
                node.CenterPivot();
            }
            t.SetSpeedScale(overrideSpeed ? 1 : speedScale);

            var tweener = t.TweenProperty(obj ?? node, property.ToString(), value, duration)
                .SetEase(EaseType)
                .SetTrans(TransType);
            if (from.HasValue)
            {
                tweener.From(from.Value);
            }
            if (Delay != 0)
            {
                tweener.SetDelay(Delay);
            }
            t.TweenCallback(Callable.From(callback));
            return t;
        }

        public static Tween AnimateIn(
            this Control node,
            double duration = .2,
            double Delay = 0,
            Action callback = null,
            Tween.EaseType EaseType = Tween.EaseType.Out,
            Tween.TransitionType TransType = Tween.TransitionType.Spring,
            bool center = true,
            bool fade = true,
            float overshoot = 1f,
            float from = 0f,
            float to = 1f
        )
        {
            node.Show();
            var t = node.AnimateTo(
                property: Control.PropertyName.Scale,
                value: (Vector2.One * to) * overshoot,
                duration: duration,
                Delay: Delay,
                from: Vector2.One * from,
                callback: callback,
                EaseType: EaseType,
                TransType: TransType,
                center: center
            );
            if (fade)
            {
                t.SetParallel();
                node.AnimateTo(
                    CanvasItem.PropertyName.Modulate,
                    value: Colors.White,
                    duration / 2,
                    Delay,
                    from: Colors.Transparent,
                    tween: t,
                    callback: callback,
                    EaseType: EaseType,
                    TransType: TransType,
                    center: center
                );
            }

            if (overshoot != 1)
            {
                // Second tween: scale from overshoot back to 1
                t.SetParallel(false);
                node.AnimateTo(
                    Control.PropertyName.Scale,
                    Vector2.One * to,
                    duration * 0.3f,
                    Delay: Delay + (duration * 0.7f),
                    from: Vector2.Zero,
                    tween: t,
                    callback: callback,
                    EaseType: Tween.EaseType.Out,
                    TransType: Tween.TransitionType.Quart,
                    center: center
                );
            }
            t.SetParallel(false).TweenCallback(Callable.From(callback));
            return t;
        }

        public static Tween AnimateInSlideDown(
            this Control node,
            double duration = .2,
            double Delay = 0,
            Action callback = null,
            Tween.EaseType EaseType = Tween.EaseType.Out,
            Tween.TransitionType TransType = Tween.TransitionType.Spring,
            bool center = true,
            Vector2? from = null,
            bool fadeIn = false
        )
        {
            Tween t = GetTween(node);
            node.Show();

            if (center)
            {
                node.CenterPivot();
            }

            t.TweenProperty(node, Control.PropertyName.Position.ToString(), Vector2.Zero, duration)
                .From(from ?? Vector2.Up * 100)
                .SetEase(EaseType)
                .SetTrans(TransType)
                .SetDelay(Delay);
            if (fadeIn)
            {
                t.SetParallel()
                    .TweenProperty(
                        node,
                        CanvasItem.PropertyName.Modulate.ToString(),
                        Colors.White,
                        duration
                    )
                    .From(Colors.Transparent)
                    .SetEase(EaseType)
                    .SetTrans(TransType)
                    .SetDelay(Delay);
            }
            else
            {
                t.SetParallel()
                    .TweenProperty(
                        node,
                        CanvasItem.PropertyName.Modulate.ToString(),
                        Colors.Transparent,
                        duration
                    )
                    .FromCurrent()
                    .SetEase(EaseType)
                    .SetTrans(TransType)
                    .SetDelay(Delay);
            }
            t.SetParallel(false).TweenCallback(Callable.From(callback));

            return t;
        }

        public static Tween AnimateInSlideDownOffset(
            this Control node,
            double duration = .2,
            double Delay = 0,
            Action callback = null,
            Tween.EaseType EaseType = Tween.EaseType.Out,
            Tween.TransitionType TransType = Tween.TransitionType.Spring,
            bool center = true,
            Vector2? from = null
        )
        {
            Tween t = GetTween(node);
            node.Show();

            if (center)
            {
                node.CenterPivot();
            }
            node.OffsetTransformEnabled = true;
            t.TweenProperty(
                    node,
                    Control.PropertyName.OffsetTransformPosition.ToString(),
                    Vector2.Zero,
                    duration
                )
                .From(from ?? Vector2.Up * 100)
                .SetEase(EaseType)
                .SetTrans(TransType)
                .SetDelay(Delay);

            t.SetParallel()
                .TweenProperty(
                    node,
                    CanvasItem.PropertyName.Modulate.ToString(),
                    Colors.White,
                    duration
                )
                .From(Colors.Transparent)
                .SetEase(EaseType)
                .SetTrans(TransType)
                .SetDelay(Delay);
            t.TweenCallback(Callable.From(callback));
            return t;
        }

        public static Tween AnimateOutSlideUp(
            this Control node,
            double duration = .2,
            double Delay = 0,
            Action callback = null,
            Tween.EaseType EaseType = Tween.EaseType.In,
            Tween.TransitionType TransType = Tween.TransitionType.Quad,
            bool center = true,
            Vector2? to = null
        )
        {
            Tween t = GetTween(node);

            if (center)
            {
                node.CenterPivot();
            }
            node.OffsetTransformEnabled = true;
            t.TweenProperty(
                    node,
                    Control.PropertyName.OffsetTransformPosition.ToString(),
                    to ?? Vector2.Up * 100,
                    duration
                )
                .From(Vector2.Zero)
                .SetEase(EaseType)
                .SetTrans(TransType)
                .SetDelay(Delay);

            t.SetParallel()
                .TweenProperty(
                    node,
                    CanvasItem.PropertyName.Modulate.ToString(),
                    Colors.Transparent,
                    duration
                )
                .From(Colors.White)
                .SetEase(EaseType)
                .SetTrans(TransType)
                .SetDelay(Delay);

            t.SetParallel(false)
                .TweenCallback(
                    Callable.From(() =>
                    {
                        node.Hide();
                        callback?.Invoke();
                    })
                );
            return t;
        }

        public static Tween GetTween(Node node)
        {
            Tween t;
            if (tweens.TryGetValue(node, out Tween value))
            {
                t = value;
                if (t != null && t.IsRunning())
                {
                    t.Kill();
                }
            }
            t = node.CreateTween();
            tweens[node] = t;
            t.SetIgnoreTimeScale();
            return t;
        }

        public static void CenterPivot(this Control node)
        {
            node.PivotOffset = node.Size / 2;
        }

        public static Tween AnimatePopInPlace(
            this Control node,
            double duration = .2,
            double Delay = 0,
            float factor = 1.1f,
            Action callback = null,
            Tween.EaseType EaseType = Tween.EaseType.Out,
            Tween.TransitionType TransType = Tween.TransitionType.Spring
        )
        {
            if (!node.Visible)
            {
                return AnimateIn(node, duration, Delay, callback, EaseType, TransType);
            }

            var t = node.CreateTween();
            t.SetIgnoreTimeScale();
            // t.SetSpeedScale((float)(1 / Engine.TimeScale));
            /* var init = node.Scale; */
            var init = Vector2.One;
            t.TweenProperty(node, "scale", init * factor, duration)
                .SetEase(EaseType)
                .SetDelay(Delay)
                .SetTrans(TransType);
            t.TweenProperty(node, "scale", init, duration);
            t.TweenCallback(Callable.From(callback));
            return t;
        }

        public static void StaggerScaleInList<T>(this List<T> list, float duration = .2f)
            where T : Control
        {
            int i = 0;
            foreach (var item in list)
            {
                item.AnimateIn(
                        duration,
                        Delay: i * duration,
                        EaseType: Tween.EaseType.Out,
                        TransType: Tween.TransitionType.Back
                    )
                    .SetIgnoreTimeScale(true);
                i++;
            }
        }

        public static Tween AnimateOut(
            this Control node,
            bool hide = true,
            double duration =  .2,
            Tween.EaseType EaseType = Tween.EaseType.Out,
            Tween.TransitionType TransType = Tween.TransitionType.Spring
        )
        {
            var t = node.CreateTween();
            t.SetIgnoreTimeScale();
            t.TweenProperty(node, "scale", node.Scale * 1.2f, duration / 2)
                .SetEase(EaseType)
                .SetTrans(TransType);
            t.TweenProperty(node, "scale", Vector2.Zero, duration)
                .SetEase(EaseType)
                .SetTrans(TransType);
            t.Finished += () =>
            {
                if (hide)
                {
                    node.Hide();
                }
            };
            return t;
        }

        public static Tween AnimateFadeOut(
            this CanvasItem node,
            bool hide = true,
            double duration = .2,
            Action callback = null
        )
        {
            var t = GetTween(node);
            t.TweenProperty(node, "modulate", Colors.Transparent, duration);
            if (hide)
            {
                t.Finished += () =>
                {
                    node.Hide();
                    callback?.Invoke();
                };
            }
            return t;
        }

        public static Vector2 Get2DPosition(this Node instance, Vector3 position)
        {
            var cam = instance.GetTree().Root.GetCamera3D();
            if (cam?.IsPositionBehind(position) ?? true)
            {
                return Vector2.One * -1000;
            }
            return cam.UnprojectPosition(position);
        }

        public static Vector2 GetCameraCenter(this Node instance)
        {
            return instance.GetViewport().GetVisibleRect().Size / 2;
        }

        // Snap a single object to the floor
        public static void SnapToFloor(this Node3D obj, float rayLength = 100f)
        {
            // Create a physics ray from the object's current position
            var spaceState = obj.GetWorld3D().DirectSpaceState;
            var start = obj.GlobalPosition;
            var end = start - new Vector3(0, rayLength, 0);

            // Perform the ray intersection
            var query = new PhysicsRayQueryParameters3D();
            query.From = start;
            query.To = end;

            if (obj is CollisionObject3D coll)
            {
                // Ignore the object itself in the raycast
                query.Exclude = new Godot.Collections.Array<Rid> { coll.GetRid() };
            }

            var result = spaceState.IntersectRay(query);

            // If we hit something, snap to that point
            if (result.Count > 0)
            {
                Vector3 hitPoint = (Vector3)result["position"];

                // // Optional: Adjust for object's local origin
                Vector3 localOrigin = obj.Position;
                Vector3 adjustedPosition = new Vector3(localOrigin.X, hitPoint.Y, localOrigin.Z);

                obj.Position = adjustedPosition;
            }
        }

        public static Vector3 MoveRandomly(this Vector3 v, float spread = 1)
        {
            var randX = GD.RandRange(-spread, spread);
            var randY = GD.RandRange(-spread, spread);
            return v + (Vector3.Up * ((float)randY)) + (Vector3.Left * ((float)randX));
        }

        // public static T PickRandom<T>(this T[] array, int from = 0, int to = -1)
        // {
        //     if (to == -1)
        //     {
        //         to = array.Length - 1;
        //     }
        //     return array[GameManager.RNG.RandiRange(from, to)];
        // }

        // public static T PickRandom<T>(this List<T> array, int from = 0, int to = -1)
        // {
        //     if (to == -1)
        //     {
        //         to = array.Count - 1;
        //     }
        //     return array[GameManager.RNG.RandiRange(from, to)];
        // }

        /// <summary>
        /// Pick "count" elements
        /// </summary>
        public static List<T> ShuffleAndTake<T>(
            this IEnumerable<T> array,
            int count = 3,
            RandomNumberGenerator rng = null
        )
        {
            rng ??= new RandomNumberGenerator();
            var shuffled = array.OrderBy(x => rng.Randi()).Take(count).ToList();
            return shuffled;
        }

        public static List<T> UniformShuffleAndTake<T>(
            this IList<T> list,
            int count,
            RandomNumberGenerator rng = null
        )
        {
            rng ??= new RandomNumberGenerator();
            if (count > list.Count())
                count = list.Count();

            var result = new List<T>(count);
            var temp = new List<T>(list); // copy so original isn't modified

            for (int i = 0; i < count; i++)
            {
                // Pick random index from i..end
                int j = rng.RandiRange(i, temp.Count - 1);

                // Swap
                (temp[i], temp[j]) = (temp[j], temp[i]);

                // Take element
                result.Add(temp[i]);
            }

            return result;
        }

        public static List<T> WeightedShuffleAndTake<T>(
            this IList<T> list,
            int count,
            Func<T, int> weightSelector, // function to get rarity weight
            RandomNumberGenerator rng = null
        )
        {
            rng ??= new RandomNumberGenerator();
            if (count > list.Count)
                count = list.Count;

            var result = new List<T>(count);
            var temp = new List<T>(list); // copy so original isn’t modified

            for (int i = 0; i < count; i++)
            {
                // Compute total weight of remaining items
                int totalWeight = temp.Sum(weightSelector);
                if (totalWeight <= 0)
                {
                    // GD.Print("No Elements With positive weight");
                    break;
                }

                // Pick a random number in [0, totalWeight)
                int roll = rng.RandiRange(0, totalWeight - 1);

                // Find which item corresponds to that roll
                int cumulative = 0;
                int chosenIndex = 0;
                for (int j = 0; j < temp.Count; j++)
                {
                    cumulative += weightSelector(temp[j]);
                    if (roll < cumulative)
                    {
                        chosenIndex = j;
                        break;
                    }
                }

                // Take element
                result.Add(temp[chosenIndex]);
                temp.RemoveAt(chosenIndex); // remove so it can't be chosen again
            }

            return result;
        }

        public static Tween SimultateDelay(
            this Node node,
            float time,
            Action callback = null,
            float speedScale = 1,
            bool overrideSpeed = true
        )
        {
            var t = node.CreateTween();

            t.SetSpeedScale(overrideSpeed ? 1 : speedScale);
            t.TweenInterval(time);
            t.TweenCallback(Callable.From(callback));
            return t;
        }

        /// <summary>
        /// Moves a node to a container located at the root level.
        /// </summary>
        /// <param name="node">The node to move.</param>
        /// <param name="containerPath">The path to the container relative to the root (e.g. "/root/Container").</param>
        public static void MoveNodeToContainer(this Node node, string containerPath)
        {
            if (node == null)
                return;

            // Get the root (SceneTree root is always "/root")
            var root = node.GetTree().Root;

            // Get the target container
            var container = root.GetNodeOrNull<Node>(containerPath);
            if (container == null)
            {
                node.LogRed($"Container not found at path: {containerPath}");
                return;
            }
            node.MoveNodeToContainer(container);
        }

        public static void MoveNodeToContainer(this Node node, Node container)
        {
            if (container == null)
            {
                node.LogRed("container is null");
                return;
            }
            // Reparent the node
            node.Reparent(container);
        }

        public static Vector2 CenterGlobalPosition(this Control node, bool ignorePivot = false)
        {
            if (ignorePivot)
            {
                return node.GlobalPosition + node.Size / 2;
            }
            return node.GlobalPosition + node.PivotOffset;
        }

        public static void SetCenterGlobalPosition(this Control node, Vector2 pos)
        {
            node.GlobalPosition = pos - node.PivotOffset;
        }

        public static async Task AnimatePop(
            this Control node,
            float scale = 2,
            float duration = .05f,
            Tween.EaseType EaseType = Tween.EaseType.Out,
            Tween.TransitionType TransType = Tween.TransitionType.Spring,
            float outSpeed = 2f,
            bool overrideSpeed = true,
            Action callback = null
        )
        {
            node.CenterPivot();
            var t = node.AnimateTo(
                "scale",
                Vector2.One * scale,
                duration: duration,
                EaseType: Tween.EaseType.InOut,
                TransType: Tween.TransitionType.Spring,
                overrideSpeed: overrideSpeed
            );
            t.SetParallel(false);
            node.AnimateTo(
                "scale",
                Vector2.One,
                tween: t,
                duration: duration * outSpeed,
                EaseType: EaseType,
                TransType: TransType,
                overrideSpeed: overrideSpeed
            );
            await t.AwaitFinished();
            callback?.Invoke();
        }

        public static bool IsSafe(this GodotObject node)
        {
            return node != null
                && GodotObject.IsInstanceValid(node)
                && !node.IsQueuedForDeletion()
                && (node is not Node n || n.IsInsideTree());
        }

        /// FROM : https://github.com/godotengine/godot-proposals/issues/3055
        /// <summary>
        /// Checks if given Property exists on <c>Base</c>
        /// </summary>
        /// <param name="Base">Current <c>GodotObject</c></param>
        /// <param name="PropertyName">Property Name</param>
        /// <returns><c>bool</c> true if given property exists on Base</returns>
        public static bool Has(this GodotObject Base, string PropertyName)
        {
            /*
            Returns the object's property list as an Godot.Collections.Array of dictionaries.
            Each Godot.Collections.Dictionary contains the following entries:
            - name is the property's name, as a string;
            - class_name is an empty StringName, unless the property is Variant.Type.Object and it inherits from a class;
            - type is the property's type, as an int (see Variant.Type);
            - hint is how the property is meant to be edited (see PropertyHint);
            - hint_string depends on the hint (see PropertyHint);
            - usage is a combination of PropertyUsageFlags.
            */
            foreach (var Property in Base.GetPropertyList())
            {
                if (Property["name"].AsString() == PropertyName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
