public enum InteractableObjectType
{
    Cube,
    Cylinder
}

public enum InteractableColor
{
    Red,
    Blue,
    Green,
    Yellow
}

public enum HandSide
{
    Unknown,
    Left,
    Right
}

public enum GrabListenerHostRole
{
    Controller,
    GrabTarget,
    /// <summary>임의 빈 GameObject에 부착. Inspector에 감시할 대상을 직접 지정합니다.</summary>
    Custom
}
