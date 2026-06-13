using BlackMagicAPI.Network;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BlackMagicAPI.Modules.Spells;

/// <summary>
/// Abstract base class for spell behavior logic.
/// Provides the core interface for spell casting functionality and initialization.
/// </summary>
public abstract class SpellLogic : MonoBehaviour, ISpell
{
    internal static Dictionary<string, List<SpellLogic>> Instances = [];

    [SerializeField]
    [Tooltip("Spelldata Fullname, (DO NOT SET)")]
    internal string? SpellDataTypeName;

    [SerializeField]
    [Tooltip("Keep item on death, (DO NOT SET)")]
    internal bool KeepOnDeath;

    /// <summary>
    /// Gets whether this instance is a prefab template or an active spell instance.
    /// </summary>
    public bool IsPrefab { get; internal set; } = true;

    /// <summary>
    /// Initializes the spell instance and sets up type references asynchronously.
    /// </summary>
    protected virtual void Awake()
    {
        StartCoroutine(CoAwake());
    }

    /// <summary>
    /// Cleans up the spell instance by removing it from tracking dictionaries when destroyed.
    /// </summary>
    protected virtual void OnDestroy()
    {
        if (SpellDataTypeName != null && Instances.TryGetValue(SpellDataTypeName, out var list))
        {
            list.Remove(this);

            if (list.Count <= 0)
            {
                Instances.Remove(SpellDataTypeName);
            }
        }
    }

    private IEnumerator CoAwake()
    {
        while (SpellDataTypeName == null)
        {
            yield return null;
        }

        float wait = 0f;
        while (IsPrefab)
        {
            wait += Time.deltaTime;
            if (wait > 5f)
            {
                yield break;
            }

            yield return null;
        }

        if (!Instances.ContainsKey(SpellDataTypeName))
        {
            Instances[SpellDataTypeName] = [];
        }
        Instances[SpellDataTypeName].Add(this);
    }

    /// <inheritdoc/>
    public void PlayerSetup(GameObject ownerobj, Vector3 fwdVector, int level) { }

    /// <summary>
    /// Contains the core spell casting logic to be implemented by derived classes.
    /// </summary>
    /// <param name="caster">The player casting the spell.</param>
    /// <param name="page">The PageController containing spell information.</param>
    /// <param name="spawnPos">The position where the spell should be spawned.</param>
    /// <param name="viewDirectionVector">The direction vector of the player's view.</param>
    /// <param name="castingLevel">The power level of the spell cast.</param>
    /// <returns>
    /// <c>true</c> if the spell was successfully cast and the page should go on cooldown;
    /// <c>false</c> if the spell failed or the page should not go on cooldown.
    /// </returns>
    /// <remarks>
    /// This is intentionally <c>virtual</c> (not <c>abstract</c>): spells compiled against
    /// BlackMagicAPI 1.x override the legacy <see cref="GameObject"/>-based overload below instead.
    /// If this were abstract, those types would be implicitly abstract and Unity would fail to
    /// instantiate them ("could not be instantiated" / "invalid vtable method slot"). The default
    /// implementation forwards to the legacy overload so old spells still run; current spells
    /// override this method directly.
    /// </remarks>
    public virtual bool CastSpell(PlayerMovement caster, PageController page, Vector3 spawnPos, Vector3 viewDirectionVector, int castingLevel)
    {
        CastSpell(caster != null ? caster.gameObject : null, page, spawnPos, viewDirectionVector, castingLevel);
        return true;
    }

    /// <summary>
    /// Legacy (BlackMagicAPI 1.x) cast signature. Spells compiled against 1.x override this
    /// <see cref="GameObject"/>-based method; it was removed in 3.x, which made those spell types fail
    /// to load entirely. Re-declaring it as a virtual no-op lets the old overrides bind so the types
    /// load, and <see cref="InvokeCastSpell"/> routes the actual cast to whichever signature the spell
    /// implements. Current spells override the <see cref="PlayerMovement"/> overload above and ignore this.
    /// </summary>
    public virtual void CastSpell(GameObject caster, PageController page, Vector3 spawnPos, Vector3 viewDirectionVector, int castingLevel) { }

    // Caches, per concrete SpellLogic type, the legacy CastSpell(GameObject, ...) MethodInfo to call,
    // or null if the type implements the current CastSpell(PlayerMovement, ...) override directly.
    private static readonly Dictionary<Type, MethodInfo?> LegacyCastCache = [];

    /// <summary>
    /// Invokes the spell's cast logic, bridging spells compiled against older BlackMagicAPI versions.
    /// </summary>
    /// <remarks>
    /// BlackMagicAPI 1.x declared <c>void CastSpell(GameObject, PageController, Vector3, Vector3, int)</c>.
    /// 3.x changed it to <c>bool CastSpell(PlayerMovement, ...)</c>. Spells built against 1.x therefore
    /// never satisfy the current abstract method, so a direct virtual call would either do nothing or throw.
    /// This dispatcher calls the modern override when present, and otherwise reflects the legacy
    /// <c>GameObject</c>-based method so old spells still function. Returns true (page goes on cooldown)
    /// when a legacy void method is used.
    /// </remarks>
    internal bool InvokeCastSpell(PlayerMovement caster, PageController page, Vector3 spawnPos, Vector3 viewDirectionVector, int castingLevel)
    {
        var type = GetType();
        if (!LegacyCastCache.TryGetValue(type, out var legacy))
        {
            var modern = type.GetMethod(nameof(CastSpell),
                [typeof(PlayerMovement), typeof(PageController), typeof(Vector3), typeof(Vector3), typeof(int)]);

            // If the modern signature isn't overridden by this type, look for the legacy GameObject-based one.
            if (modern == null || modern.DeclaringType == typeof(SpellLogic))
            {
                legacy = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(m =>
                        m.Name == nameof(CastSpell) &&
                        m.GetParameters() is { Length: 5 } p &&
                        p[0].ParameterType == typeof(GameObject));
            }
            else
            {
                legacy = null;
            }

            LegacyCastCache[type] = legacy;
        }

        if (legacy != null)
        {
            var result = legacy.Invoke(this,
                [caster != null ? caster.gameObject : null, page, spawnPos, viewDirectionVector, castingLevel]);
            return result is bool b ? b : true;
        }

        return CastSpell(caster, page, spawnPos, viewDirectionVector, castingLevel);
    }

    /// <summary>
    /// Virtual method for handling item-specific usage logic for spell page.
    /// </summary>
    /// <param name="itemOwner">The player using the item</param>
    /// <param name="page">The PageController containing spell information</param>
    /// <remarks>
    /// Note that this code executes within the SpellLogic prefab, so avoid modifying anything within the prefab itself!
    /// </remarks>
    public virtual void OnPageItemUse(PlayerMovement itemOwner, PageController page) { }

    /// <summary>
    /// Called automatically when a spell prefab is created programmatically.
    /// Allows for custom initialization of spell prefabs.
    /// </summary>
    /// <param name="prefab">The GameObject of the created spell prefab.</param>
    public virtual void OnPrefabCreatedAutomatically(GameObject prefab) { }

    /// <summary>
    /// Castor writes data to a <see cref="DataWriter"/> for serialization to send to clients.
    /// </summary>
    /// <param name="dataWriter">The writer used to serialize data.</param>
    /// <param name="page">The page controller associated with the data.</param>
    /// <param name="caster">The player GameObject to serialize.</param>
    /// <param name="spawnPos">The spawn position of the player.</param>
    /// <param name="viewDirectionVector">The view direction of the player.</param>
    /// <param name="level">The current level or stage.</param>
    public virtual void WriteData(DataWriter dataWriter, PageController page, PlayerMovement caster, Vector3 spawnPos, Vector3 viewDirectionVector, int level) { }

    /// <summary>
    /// Clients including Castor Synchronizes of values from WriteData received from Castor.
    /// </summary>
    /// <param name="values">An array of objects containing the data to sync from WriteData.</param>
    public virtual void SyncData(object[] values) { }

    /// <summary>
    /// Cleans up and destroys the spell GameObject.
    /// </summary>
    public void DisposeSpell()
    {
        Destroy(gameObject);
    }
}
