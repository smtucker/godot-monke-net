using MonkeNet.Shared;

namespace MonkeNet.Client;

public interface IInterpolatedEntity
{
    public void HandleStateInterpolation(IEntityStateData past, IEntityStateData future, float interpolationFactor);
}