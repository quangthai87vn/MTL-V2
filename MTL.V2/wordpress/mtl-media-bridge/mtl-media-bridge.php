<?php
/**
 * Plugin Name: MTL Media Bridge
 * Description: Cho phép MTL.V2 duyệt và tải ảnh vào WordPress Media Library bằng WooCommerce Consumer Key/Secret.
 * Version: 1.0.0
 * Author: MỸ THO LAPTOP
 * Requires Plugins: woocommerce
 */

defined( 'ABSPATH' ) || exit;

add_action(
	'rest_api_init',
	static function () {
		register_rest_route(
			'wc-mtl/v1',
			'/media',
			array(
				array(
					'methods'             => WP_REST_Server::READABLE,
					'callback'            => 'mtl_media_bridge_list',
					'permission_callback' => 'mtl_media_bridge_can_upload',
					'args'                => array(
						'page'     => array( 'default' => 1, 'sanitize_callback' => 'absint' ),
						'per_page' => array( 'default' => 30, 'sanitize_callback' => 'absint' ),
						'search'   => array( 'default' => '', 'sanitize_callback' => 'sanitize_text_field' ),
					),
				),
				array(
					'methods'             => WP_REST_Server::CREATABLE,
					'callback'            => 'mtl_media_bridge_upload',
					'permission_callback' => 'mtl_media_bridge_can_upload',
				),
			)
		);
	}
);

function mtl_media_bridge_can_upload() {
	return current_user_can( 'upload_files' );
}

function mtl_media_bridge_list( WP_REST_Request $request ) {
	$page     = max( 1, (int) $request->get_param( 'page' ) );
	$per_page = min( 60, max( 1, (int) $request->get_param( 'per_page' ) ) );
	$search   = (string) $request->get_param( 'search' );
	$query    = new WP_Query(
		array(
			'post_type'              => 'attachment',
			'post_status'            => 'inherit',
			'post_mime_type'         => 'image',
			'posts_per_page'         => $per_page,
			'paged'                  => $page,
			's'                      => $search,
			'orderby'                => 'date',
			'order'                  => 'DESC',
			'no_found_rows'          => false,
			'update_post_meta_cache' => true,
		)
	);

	$items = array_map( 'mtl_media_bridge_prepare_item', $query->posts );
	return rest_ensure_response(
		array(
			'items'         => $items,
			'total'         => (int) $query->found_posts,
			'page'          => $page,
			'pageSize'      => $per_page,
			'uploadEnabled' => true,
			'source'        => 'wordpress',
		)
	);
}

function mtl_media_bridge_upload() {
	if ( empty( $_FILES['file'] ) ) {
		return new WP_Error( 'mtl_media_missing_file', 'Không tìm thấy tệp ảnh.', array( 'status' => 400 ) );
	}

	require_once ABSPATH . 'wp-admin/includes/file.php';
	require_once ABSPATH . 'wp-admin/includes/image.php';
	require_once ABSPATH . 'wp-admin/includes/media.php';

	$attachment_id = media_handle_upload( 'file', 0, array(), array( 'test_form' => false ) );
	if ( is_wp_error( $attachment_id ) ) {
		return $attachment_id;
	}

	return rest_ensure_response( mtl_media_bridge_prepare_item( get_post( $attachment_id ) ) );
}

function mtl_media_bridge_prepare_item( WP_Post $attachment ) {
	$full      = wp_get_attachment_image_src( $attachment->ID, 'full' );
	$thumbnail = wp_get_attachment_image_src( $attachment->ID, 'medium' );
	$metadata  = wp_get_attachment_metadata( $attachment->ID );

	return array(
		'id'           => (int) $attachment->ID,
		'url'          => $full ? $full[0] : wp_get_attachment_url( $attachment->ID ),
		'thumbnailUrl' => $thumbnail ? $thumbnail[0] : ( $full ? $full[0] : null ),
		'title'        => get_the_title( $attachment->ID ),
		'altText'      => get_post_meta( $attachment->ID, '_wp_attachment_image_alt', true ),
		'mimeType'     => get_post_mime_type( $attachment->ID ),
		'width'        => isset( $metadata['width'] ) ? (int) $metadata['width'] : null,
		'height'       => isset( $metadata['height'] ) ? (int) $metadata['height'] : null,
		'createdAt'    => mysql_to_rfc3339( $attachment->post_date_gmt ),
	);
}
