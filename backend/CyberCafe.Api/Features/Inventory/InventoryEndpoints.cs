namespace CyberCafe.Api.Features.Inventory;

public static class InventoryEndpoints
{
	// Đăng ký API đọc tồn kho.
	public static IEndpointRouteBuilder MapInventoryEndpoints(
		this IEndpointRouteBuilder endpoints)
	{
		endpoints.MapGet(
			"/api/admin/inventory",
			async (
				InventoryQueries queries,
				CancellationToken cancellationToken) =>
			{
				var items = await queries.GetInventoryAsync(
					cancellationToken);

				return Results.Ok(items);
			})
			.WithName("GetAdminInventory")
			.WithTags("Inventory")
			.Produces<IReadOnlyList<InventoryItemResponse>>(
				StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status403Forbidden)
			// Chỉ tài khoản đã xác thực thuộc Admin/Staff.
			.RequireAuthorization(policy => policy
				.RequireAuthenticatedUser()
				.RequireRole("Admin", "Staff"));

		return endpoints;
	}
}