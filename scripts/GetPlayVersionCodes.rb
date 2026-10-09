require 'google/apis/androidpublisher_v3'
require 'googleauth'
require 'json'

api = Google::Apis::AndroidpublisherV3::AndroidPublisherService.new
File.open(ENV.fetch('UNITY_RELEASE_PLAY_KEY')) do |key|
  api.authorization = Google::Auth::ServiceAccountCredentials.make_creds(
    json_key_io: key, scope: 'https://www.googleapis.com/auth/androidpublisher')
end
package = ENV.fetch('UNITY_RELEASE_PACKAGE_NAME')
edit = api.insert_edit(package)
begin
  codes = (api.list_edit_bundles(package, edit.id).bundles || []).map(&:version_code)
  codes += (api.list_edit_apks(package, edit.id).apks || []).map(&:version_code)
  (api.list_edit_tracks(package, edit.id).tracks || []).each do |track|
    codes += (track.releases || []).flat_map { |release| release.version_codes || [] }
  end
  puts JSON.generate(HighestVersionCode: codes.compact.map(&:to_i).max || 0)
ensure
  api.delete_edit(package, edit.id)
end
