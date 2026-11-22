namespace Ims.Api.Domain;

/// <summary>The five roles. Exactly one per user. Stored as text in the database (e.g. "ADMIN").</summary>
public enum Role { ADMIN, COMPONENT_TRACKER, RECEIVE_USER, DELIVERY_USER, REPORT_VIEWER }

/// <summary>What a point is used for. A TRACK point is used when tracking components, etc.</summary>
public enum PointType { TRACK, RECEIVE, DELIVERY }

/// <summary>The kind of row in the inventory history.</summary>
public enum EventType { COMPONENT_TRACKED, RECEIVED, DELIVERED, REVERSED }
