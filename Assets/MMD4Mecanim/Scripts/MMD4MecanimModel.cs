// - Don't upload model data, motion data, audio tracks, and codes to public space(github, etc...) without permission.
using UnityEngine;

[ExecuteInEditMode ()] // for Morph
public class MMD4MecanimModel : MMD4MecanimModelImpl
{
	public MMD4MecanimBone GetRootBone()
	{
		return GetRootBoneImpl() as MMD4MecanimBone;
	}

	public MMD4MecanimBone GetBone( int boneID )
	{
		return GetBone( boneID ) as MMD4MecanimBone;
	}

	protected override MMD4MecanimBoneImpl AddComponentBoneImpl( GameObject gameObject )
	{
		if( gameObject != null ) {
			return gameObject.AddComponent< MMD4MecanimBone >();
		} else {
			return null;
		}
	}

	protected override MMD4MecanimIKTargetImpl AddComponentIKTargetImpl( GameObject gameObject )
	{
		if( gameObject != null ) {
			return gameObject.AddComponent< MMD4MecanimIKTarget >();
		} else {
			return null;
		}
	}
}
